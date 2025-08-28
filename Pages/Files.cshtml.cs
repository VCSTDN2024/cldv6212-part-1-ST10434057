
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ABC.Retail.StorageApp.Services;

public class FilesModel : PageModel
{
    private readonly AzureStorageService _svc;
    public FilesModel(AzureStorageService svc) => _svc = svc;

    public List<string> Files { get; set; } = new();

    public async Task OnGetAsync()
    {
        Files = await _svc.ListContractsAsync();
    }

    public async Task<IActionResult> OnPostAsync(IFormFile file)
    {
        if (file != null && file.Length > 0)
        {
            using var stream = file.OpenReadStream();
            await _svc.UploadContractAsync(file.FileName, stream);
            await _svc.EnqueueMessageAsync($"Uploaded contract '{file.FileName}'");
        }
        return RedirectToPage();
    }

    public async Task<IActionResult> OnGetDownloadAsync(string name)
    {
        var stream = await _svc.DownloadContractAsync(name);
        return File(stream, "application/octet-stream", name);
    }
}
