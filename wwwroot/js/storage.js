window.financeStorage = {

    // The language preference is kept outside IndexedDB so backup restores do not overwrite it.
    getLanguage() {
        try {
            return localStorage.getItem("finansim-language") || "tr";
        } catch {
            return "tr";
        }
    },

    setLanguage(language) {
        const selected = language === "en" ? "en" : "tr";
        try {
            localStorage.setItem("finansim-language", selected);
        } catch {
            // The current session can still use the selected language.
        }
        document.documentElement.lang = selected;
    },

    database: null,

    async openDatabase() {

        if (this.database)
            return this.database;

        return new Promise((resolve, reject) => {

            const request = indexedDB.open("FinansimDB", 1);

            request.onupgradeneeded = function (event) {

                const db = event.target.result;

                if (!db.objectStoreNames.contains("appData")) {
                    db.createObjectStore("appData");
                }
            };

            request.onsuccess = (event) => {
                this.database = event.target.result;
                resolve(this.database);
            };

            request.onerror = () => reject(request.error);

        });
    },

    async save(key, value) {

        const db = await this.openDatabase();

        return new Promise((resolve, reject) => {

            const transaction = db.transaction("appData", "readwrite");

            transaction.objectStore("appData").put(value, key);

            transaction.oncomplete = () => resolve();

            transaction.onerror = () => reject(transaction.error);

        });
    },

    async load(key) {

        const db = await this.openDatabase();

        return new Promise((resolve, reject) => {

            const transaction = db.transaction("appData", "readonly");

            const request = transaction.objectStore("appData").get(key);

            request.onsuccess = () => resolve(request.result ?? null);

            request.onerror = () => reject(request.error);

        });
    },

    async delete(key) {

        const db = await this.openDatabase();

        return new Promise((resolve, reject) => {

            const transaction = db.transaction("appData", "readwrite");

            transaction.objectStore("appData").delete(key);

            transaction.oncomplete = () => resolve();

            transaction.onerror = () => reject(transaction.error);

        });
    }
,

    // IndexedDB kayıtlarını JSON yedeğine ham JSON metinleriyle ekler.
    // Böylece JavaScript number dönüşümü finansal ondalık hassasiyetini değiştirmez.
    async exportBackup() {
        const db = await this.openDatabase();

        return new Promise((resolve, reject) => {
            const transaction = db.transaction("appData", "readonly");
            const store = transaction.objectStore("appData");
            const keysRequest = store.getAllKeys();
            const valuesRequest = store.getAll();
            let keys = null;
            let values = null;

            keysRequest.onsuccess = () => { keys = keysRequest.result; };
            valuesRequest.onsuccess = () => { values = valuesRequest.result; };
            keysRequest.onerror = () => reject(keysRequest.error);
            valuesRequest.onerror = () => reject(valuesRequest.error);

            transaction.oncomplete = () => {
                try {
                    const rawEntries = [];
                    (keys || []).forEach((key, index) => {
                        const rawValue = values[index];
                        let rawJson;

                        if (typeof rawValue === "string") {
                            try {
                                JSON.parse(rawValue);
                                rawJson = rawValue;
                            } catch {
                                rawJson = JSON.stringify(rawValue);
                            }
                        } else {
                            rawJson = JSON.stringify(rawValue);
                        }

                        if (rawJson === undefined) {
                            throw new Error(`'${String(key)}' kaydı JSON olarak yedeklenemedi.`);
                        }

                        rawEntries.push(`${JSON.stringify(String(key))}:${rawJson}`);
                    });

                    if (!(keys || []).some(key => String(key) === "finance-transactions")) {
                        rawEntries.push('"finance-transactions":[]');
                    }

                    const metadata = JSON.stringify({
                        application: "Finansim",
                        backupVersion: 1,
                        exportedAt: new Date().toISOString()
                    });

                    const backupJson = metadata.slice(0, -1) + `,"data":{${rawEntries.join(",")}}}`;
                    resolve(backupJson);
                } catch (error) {
                    reject(error);
                }
            };

            transaction.onerror = () => reject(transaction.error || new Error("Yedek verileri okunamadı."));
            transaction.onabort = () => reject(transaction.error || new Error("Yedek okuma işlemi iptal edildi."));
        });
    },

    // C# tarafında doğrulanan ham JSON kayıtlarını tek bir IndexedDB transaction'ında geri yükler.
    async importBackup(rawData) {
        if (!rawData || typeof rawData !== "object" || Array.isArray(rawData) ||
            typeof rawData["finance-settings"] !== "string" ||
            typeof rawData["finance-transactions"] !== "string") {
            throw new Error("Yedek dosyasının veri yapısı Finansim ile uyumlu değil.");
        }

        const db = await this.openDatabase();

        return new Promise((resolve, reject) => {
            const transaction = db.transaction("appData", "readwrite");
            const store = transaction.objectStore("appData");

            store.clear();
            for (const [key, rawJson] of Object.entries(rawData)) {
                if (typeof rawJson !== "string") {
                    transaction.abort();
                    reject(new Error(`'${key}' kaydı geçerli JSON metni değil.`));
                    return;
                }
                store.put(rawJson, key);
            }

            transaction.oncomplete = () => resolve();
            transaction.onerror = () => reject(transaction.error || new Error("Yedek geri yüklenemedi."));
            transaction.onabort = () => reject(transaction.error || new Error("Yedek geri yükleme işlemi iptal edildi."));
        });
    },

    // JSON yedeğini kullanıcıya indirir.
    downloadBackup(backupJson, fileName) {
        const blob = new Blob([backupJson], { type: "application/json;charset=utf-8" });
        const url = URL.createObjectURL(blob);
        const link = document.createElement("a");
        link.href = url;
        link.download = fileName;
        link.style.display = "none";
        document.body.appendChild(link);
        link.click();
        link.remove();
        window.setTimeout(() => URL.revokeObjectURL(url), 1000);
    }
};

// Apply the saved language as early as possible when the app starts.
try {
    document.documentElement.lang = window.financeStorage.getLanguage();
} catch {
    document.documentElement.lang = "tr";
}
