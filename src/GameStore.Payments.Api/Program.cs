using GameStore.Contracts.Events;
using GameStore.Payments.Application.Services;
using GameStore.Payments.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddHealthChecks();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddHttpClient("CatalogApi", client =>
{
    var url = builder.Configuration["Services:CatalogApiUrl"] ?? "http://catalog-api:80";
    client.BaseAddress = new Uri(url);
});
builder.Services.AddHttpClient("UserApi", client =>
{
    var url = builder.Configuration["Services:UserApiUrl"] ?? "http://user-api:80";
    client.BaseAddress = new Uri(url);
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "GameStore - PaymentsAPI",
        Version = "v1",
        Description = "Microsserviço de Processamento de Pagamentos (consome OrderPlaceEvent e publica PaymentProcessedEvent)."
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "GameStore PaymentsAPI v1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "GameStore PaymentsAPI - Swagger UI";
});

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();
app.MapHealthChecks("/health").ExcludeFromDescription();

var group = app.MapGroup("/api/payments").WithTags("1. Pagamentos Processados");

group.MapGet("/", async (IPaymentService paymentService, CancellationToken ct) =>
{
    var transactions = await paymentService.GetAllAsync(ct);
    return Results.Ok(transactions);
})
.WithName("GetAllPayments")
.WithSummary("Listar todas as transações de pagamento processadas")
.Produces<IEnumerable<PaymentResponse>>(StatusCodes.Status200OK);

group.MapGet("/user/{userId:guid}", async (Guid userId, IPaymentService paymentService, CancellationToken ct) =>
{
    var transactions = await paymentService.GetByUserIdAsync(userId, ct);
    return Results.Ok(transactions);
})
.WithName("GetPaymentsByUser")
.WithSummary("Listar transações de pagamento de um usuário específico")
.Produces<IEnumerable<PaymentResponse>>(StatusCodes.Status200OK);

group.MapPost("/simulate", async (
    [FromBody] OrderPlaceEvent request,
    IPaymentService paymentService,
    CancellationToken ct) =>
{
    var result = await paymentService.ProcessOrderPaymentAsync(request, ct);
    return Results.Ok(result);
})
.WithName("SimulatePayment")
.WithSummary("Simular manualmente o processamento de um pedido e publicar PaymentProcessedEvent")
.Produces<PaymentResponse>(StatusCodes.Status200OK);

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<PaymentsDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
}

app.Run();

public partial class Program { }

