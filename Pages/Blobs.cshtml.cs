
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ABC.Retail.StorageApp.Services;

public class BlobsModel : PageModel
{
    private readonly AzureStorageService _svc;
    public BlobsModel(AzureStorageService svc) => _svc = svc;

    public List<string> ImageUrls { get; set; } = new();

    public async Task OnGetAsync()
    {
        ImageUrls = await _svc.ListImageUrlsAsync();
    }

    public async Task<IActionResult> OnPostAsync(IFormFile file)
    {
        if (file != null && file.Length > 0)
        {
            using var stream = file.OpenReadStream();
            var url = await _svc.UploadImageAsync(file.FileName, stream, file.ContentType);
            await _svc.EnqueueMessageAsync($"Uploaded image '{file.FileName}'");
        }
        return RedirectToPage();
    }
}
