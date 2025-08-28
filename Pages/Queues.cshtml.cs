
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ABC.Retail.StorageApp.Services;

public class QueuesModel : PageModel
{
    private readonly AzureStorageService _svc;
    public QueuesModel(AzureStorageService svc) => _svc = svc;

    public List<string> Messages { get; set; } = new();
    [BindProperty(SupportsGet = true)] public int Count { get; set; } = 10;
    public int RemovedCount { get; set; } = 0;

    public async Task OnGetAsync()
    {
        Messages = await _svc.PeekMessagesAsync(Count);
    }

    public async Task<IActionResult> OnPostAddAsync(string messageText)
    {
        if (!string.IsNullOrWhiteSpace(messageText))
            await _svc.EnqueueMessageAsync(messageText);
        return RedirectToPage(new { Count });
    }

    public async Task<IActionResult> OnPostDequeueAsync(int count)
    {
        RemovedCount = await _svc.DequeueAndDeleteAsync(count);
        Messages = await _svc.PeekMessagesAsync(Count);
        return Page();
    }
}
