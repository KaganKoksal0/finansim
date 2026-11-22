using Microsoft.JSInterop;
using System.Text.Json;

namespace Finansim.Services
{
    public class StorageService
    {
        private readonly IJSRuntime _js;

        private readonly JsonSerializerOptions _options = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public StorageService(IJSRuntime js)
        {
            _js = js;
        }

        public async Task SaveAsync<T>(string key, T data)
        {
            string json = JsonSerializer.Serialize(data, _options);

            await _js.InvokeVoidAsync(
                "financeStorage.save",
                key,
                json
            );
        }

        public async Task<T?> LoadAsync<T>(string key)
        {
            string? json = await _js.InvokeAsync<string?>(
                "financeStorage.load",
                key
            );

            if (string.IsNullOrEmpty(json))
                return default;

            return JsonSerializer.Deserialize<T>(json, _options);
        }

        public async Task DeleteAsync(string key)
        {
            await _js.InvokeVoidAsync(
                "financeStorage.delete",
                key
            );
        }

        public async Task<string> ExportBackupAsync()
        {
            try
            {
                return await _js.InvokeAsync<string>("financeStorage.exportBackup");
            }
            catch (JSException)
            {
                throw;
            }
        }

        public async Task ImportBackupAsync(Dictionary<string, string> rawData)
        {
            try
            {
                await _js.InvokeVoidAsync("financeStorage.importBackup", rawData);
            }
            catch (JSException)
            {
                throw;
            }
        }

        public async Task DownloadBackupAsync(string backupJson, string fileName)
        {
            try
            {
                await _js.InvokeVoidAsync("financeStorage.downloadBackup", backupJson, fileName);
            }
            catch (JSException)
            {
                throw;
            }
        }

    }
}