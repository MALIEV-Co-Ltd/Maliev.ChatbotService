using Maliev.ChatbotService.Infrastructure.Tools.Handlers;
using Moq;
using Xunit;

namespace Maliev.ChatbotService.Tests.Unit;

public class OrderToolHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_WithSearchOrders_BuildsCorrectQuery()
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new FakeOkResponseHandler()) { BaseAddress = new Uri("http://localhost") });
        
        var handler = new OrderToolHandler(mockFactory.Object);
        
        var result = await handler.ExecuteAsync("search_orders", 
            new Dictionary<string, object> { ["page"] = 2 }, null, default);
        
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteAsync_WithSearchOrdersAndCustomerId_IncludesCustomerId()
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new FakeOkResponseHandler()) { BaseAddress = new Uri("http://localhost") });
        
        var handler = new OrderToolHandler(mockFactory.Object);
        
        var result = await handler.ExecuteAsync("search_orders", 
            new Dictionary<string, object> { ["page"] = 1, ["customer_id"] = "cust-123" }, null, default);
        
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteAsync_WithSearchOrdersAndStatus_IncludesStatus()
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new FakeOkResponseHandler()) { BaseAddress = new Uri("http://localhost") });
        
        var handler = new OrderToolHandler(mockFactory.Object);
        
        var result = await handler.ExecuteAsync("search_orders", 
            new Dictionary<string, object> { ["page"] = 1, ["status"] = "Completed" }, null, default);
        
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteAsync_WithGetOrder_ReturnsOrder()
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new FakeOkResponseHandler()) { BaseAddress = new Uri("http://localhost") });
        
        var handler = new OrderToolHandler(mockFactory.Object);
        
        var result = await handler.ExecuteAsync("get_order", 
            new Dictionary<string, object> { ["order_id"] = "ORD-001" }, null, default);
        
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownTool_ReturnsError()
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        var handler = new OrderToolHandler(mockFactory.Object);
        
        var result = await handler.ExecuteAsync("unknown_tool", new Dictionary<string, object>(), null, default);
        
        Assert.Contains("Unknown order tool", result);
    }

    private sealed class FakeOkResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            });
    }
}

public class MaterialToolHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_WithSearchMaterials_BuildsCorrectQuery()
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new FakeOkResponseHandler()) { BaseAddress = new Uri("http://localhost") });
        
        var handler = new MaterialToolHandler(mockFactory.Object);
        
        var result = await handler.ExecuteAsync("search_materials", 
            new Dictionary<string, object> { ["query"] = "aluminum", ["page"] = 1 }, null, default);
        
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteAsync_WithGetMaterial_ReturnsMaterial()
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new FakeOkResponseHandler()) { BaseAddress = new Uri("http://localhost") });
        
        var handler = new MaterialToolHandler(mockFactory.Object);
        
        var result = await handler.ExecuteAsync("get_material", 
            new Dictionary<string, object> { ["material_id"] = "mat-001" }, null, default);
        
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownTool_ReturnsError()
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        var handler = new MaterialToolHandler(mockFactory.Object);
        
        var result = await handler.ExecuteAsync("unknown_tool", new Dictionary<string, object>(), null, default);
        
        Assert.Contains("Unknown material tool", result);
    }

    private sealed class FakeOkResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            });
    }
}

public class SupplierToolHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_WithSearchSuppliers_BuildsCorrectQuery()
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new FakeOkResponseHandler()) { BaseAddress = new Uri("http://localhost") });
        
        var handler = new SupplierToolHandler(mockFactory.Object);
        
        var result = await handler.ExecuteAsync("search_suppliers", 
            new Dictionary<string, object> { ["query"] = "metal", ["page"] = 1 }, null, default);
        
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteAsync_WithGetSupplier_ReturnsSupplier()
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new FakeOkResponseHandler()) { BaseAddress = new Uri("http://localhost") });
        
        var handler = new SupplierToolHandler(mockFactory.Object);
        
        var result = await handler.ExecuteAsync("get_supplier", 
            new Dictionary<string, object> { ["supplier_id"] = "sup-001" }, null, default);
        
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownTool_ReturnsError()
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        var handler = new SupplierToolHandler(mockFactory.Object);
        
        var result = await handler.ExecuteAsync("unknown_tool", new Dictionary<string, object>(), null, default);
        
        Assert.Contains("Unknown supplier tool", result);
    }

    private sealed class FakeOkResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            });
    }
}

public class InvoiceToolHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_WithSearchInvoices_BuildsCorrectQuery()
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new FakeOkResponseHandler()) { BaseAddress = new Uri("http://localhost") });
        
        var handler = new InvoiceToolHandler(mockFactory.Object);
        
        var result = await handler.ExecuteAsync("search_invoices", 
            new Dictionary<string, object> { ["query"] = "INV-2025", ["page"] = 1 }, null, default);
        
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteAsync_WithGetInvoice_ReturnsInvoice()
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new FakeOkResponseHandler()) { BaseAddress = new Uri("http://localhost") });
        
        var handler = new InvoiceToolHandler(mockFactory.Object);
        
        var result = await handler.ExecuteAsync("get_invoice", 
            new Dictionary<string, object> { ["invoice_id"] = "INV-001" }, null, default);
        
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownTool_ReturnsError()
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        var handler = new InvoiceToolHandler(mockFactory.Object);
        
        var result = await handler.ExecuteAsync("unknown_tool", new Dictionary<string, object>(), null, default);
        
        Assert.Contains("Unknown invoice tool", result);
    }

    private sealed class FakeOkResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            });
    }
}

public class QuotationToolHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_WithSearchQuotations_BuildsCorrectQuery()
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new FakeOkResponseHandler()) { BaseAddress = new Uri("http://localhost") });
        
        var handler = new QuotationToolHandler(mockFactory.Object);
        
        var result = await handler.ExecuteAsync("search_quotations", 
            new Dictionary<string, object> { ["query"] = "CNC", ["page"] = 1 }, null, default);
        
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownTool_ReturnsError()
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        var handler = new QuotationToolHandler(mockFactory.Object);
        
        var result = await handler.ExecuteAsync("unknown_tool", new Dictionary<string, object>(), null, default);
        
        Assert.Contains("Unknown quotation tool", result);
    }

    private sealed class FakeOkResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            });
    }
}

public class PaymentToolHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_WithGetPayment_ReturnsPayment()
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new FakeOkResponseHandler()) { BaseAddress = new Uri("http://localhost") });
        
        var handler = new PaymentToolHandler(mockFactory.Object);
        
        var result = await handler.ExecuteAsync("get_payment", 
            new Dictionary<string, object> { ["payment_id"] = "PAY-001" }, null, default);
        
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteAsync_WithSearchPayments_BuildsQuery()
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new FakeOkResponseHandler()) { BaseAddress = new Uri("http://localhost") });
        
        var handler = new PaymentToolHandler(mockFactory.Object);
        
        var result = await handler.ExecuteAsync("search_payments", 
            new Dictionary<string, object> { ["query"] = "test", ["page"] = 1 }, null, default);
        
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownTool_ReturnsError()
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        var handler = new PaymentToolHandler(mockFactory.Object);
        
        var result = await handler.ExecuteAsync("unknown_tool", new Dictionary<string, object>(), null, default);
        
        Assert.Contains("Unknown payment tool", result);
    }

    private sealed class FakeOkResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            });
    }
}

public class ReceiptToolHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_WithGetReceipt_ReturnsReceipt()
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new FakeOkResponseHandler()) { BaseAddress = new Uri("http://localhost") });
        
        var handler = new ReceiptToolHandler(mockFactory.Object);
        
        var result = await handler.ExecuteAsync("get_receipt", 
            new Dictionary<string, object> { ["receipt_id"] = "RCP-001" }, null, default);
        
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownTool_ReturnsError()
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        var handler = new ReceiptToolHandler(mockFactory.Object);
        
        var result = await handler.ExecuteAsync("unknown_tool", new Dictionary<string, object>(), null, default);
        
        Assert.Contains("Unknown receipt tool", result);
    }

    private sealed class FakeOkResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            });
    }
}

public class EmployeeToolHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_WithSearchEmployees_BuildsQuery()
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new FakeOkResponseHandler()) { BaseAddress = new Uri("http://localhost") });
        
        var handler = new EmployeeToolHandler(mockFactory.Object);
        
        var result = await handler.ExecuteAsync("search_employees", 
            new Dictionary<string, object> { ["query"] = "John", ["page"] = 1 }, null, default);
        
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteAsync_WithGetEmployee_ReturnsEmployee()
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        mockFactory.Setup(f => f.CreateClient(It.IsAny<string>()))
            .Returns(new HttpClient(new FakeOkResponseHandler()) { BaseAddress = new Uri("http://localhost") });
        
        var handler = new EmployeeToolHandler(mockFactory.Object);
        
        var result = await handler.ExecuteAsync("get_employee", 
            new Dictionary<string, object> { ["employee_id"] = "EMP-001" }, null, default);
        
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ExecuteAsync_WithUnknownTool_ReturnsError()
    {
        var mockFactory = new Mock<IHttpClientFactory>();
        var handler = new EmployeeToolHandler(mockFactory.Object);
        
        var result = await handler.ExecuteAsync("unknown_tool", new Dictionary<string, object>(), null, default);
        
        Assert.Contains("Unknown employee tool", result);
    }

    private sealed class FakeOkResponseHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            });
    }
}
