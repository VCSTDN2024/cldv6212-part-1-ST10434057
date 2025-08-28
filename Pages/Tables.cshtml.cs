
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ABC.Retail.StorageApp.Services;
using ABC.Retail.StorageApp.Models;

public class TablesModel : PageModel
{
    private readonly AzureStorageService _svc;

    public TablesModel(AzureStorageService svc) => _svc = svc;

    public List<CustomerEntity> Customers { get; set; } = new();
    public List<ProductEntity> Products { get; set; } = new();

    public async Task OnGetAsync()
    {
        Customers = await _svc.GetCustomersAsync();
        Products = await _svc.GetProductsAsync();
    }

    public async Task<IActionResult> OnPostAddCustomerAsync(string FirstName, string LastName, string Email, string Phone)
    {
        await _svc.AddCustomerAsync(new CustomerEntity { FirstName = FirstName, LastName = LastName, Email = Email, Phone = Phone });
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddProductAsync(string Name, string Sku, decimal Price, int Stock)
    {
        await _svc.AddProductAsync(new ProductEntity { Name = Name, Sku = Sku, Price = Price, Stock = Stock });
        return RedirectToPage();
    }
}
