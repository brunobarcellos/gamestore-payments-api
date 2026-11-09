using GameStore.Contracts.Events;
using GameStore.Payments.Application.Services;
using GameStore.Payments.Domain.Entities;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameStore.Payments.Infrastructure;

public class PaymentsDbContext : DbContext
{
    public DbSet<PaymentTransaction> Transactions => Set<PaymentTransaction>();

    public PaymentsDbContext(DbContextOptions<PaymentsDbContext> options) : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PaymentTransaction>(builder =>
        {
            builder.ToTable("PaymentTransactions");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.OrderId).IsRequired();
            builder.Property(p => p.UserId).IsRequired();
            builder.Property(p => p.GameId).IsRequired();
            builder.Property(p => p.Price).HasPrecision(18, 2).IsRequired();
            builder.Property(p => p.Currency).HasMaxLength(10).IsRequired();
            builder.Property(p => p.Status).HasMaxLength(30).IsRequired();
            builder.Property(p => p.RejectionReason).HasMaxLength(500);
            builder.Property(p => p.UserEmail).HasMaxLength(254);
            builder.Property(p => p.GameTitle).HasMaxLength(200);
            builder.Property(p => p.ProcessedAt).IsRequired();
        });
    }
}

public class PaymentRepository : IPaymentRepository
{
    private readonly PaymentsDbContext _context;

    public PaymentRepository(PaymentsDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(PaymentTransaction transaction, CancellationToken cancellationToken = default)
        => await _context.Transactions.AddAsync(transaction, cancellationToken);

    public async Task<IEnumerable<PaymentTransaction>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Transactions.OrderByDescending(t => t.ProcessedAt).ToListAsync(cancellationToken);

    public async Task<IEnumerable<PaymentTransaction>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => await _context.Transactions.Where(t => t.UserId == userId).OrderByDescending(t => t.ProcessedAt).ToListAsync(cancellationToken);

    public async Task<PaymentTransaction?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Transactions.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public async Task<int> CommitAsync(CancellationToken cancellationToken = default)
        => await _context.SaveChangesAsync(cancellationToken);
}

public class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";
    public string Host { get; set; } = "localhost";
    public ushort Port { get; set; } = 5672;
    public string VirtualHost { get; set; } = "/";
    public string Username { get; set; } = "guest";
    public string Password { get; set; } = "guest";
    public PaymentQueueOptions Queues { get; set; } = new();
}

public class PaymentQueueOptions
{
    public string OrderPlace { get; set; } = "OrderPlaceEvent";
    public string PaymentProcessed { get; set; } = "PaymentProcessedEvent";
}

public class MassTransitEventPublisher : IEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;
    private readonly ILogger<MassTransitEventPublisher> _logger;

    public MassTransitEventPublisher(
        IPublishEndpoint publishEndpoint,
        ILogger<MassTransitEventPublisher> logger)
    {
        _publishEndpoint = publishEndpoint;
        _logger = logger;
    }

    public async Task PublishAsync<T>(T message, CancellationToken cancellationToken = default) where T : class
    {
        await _publishEndpoint.Publish(message, cancellationToken);
        _logger.LogInformation("Evento {EventType} publicado no RabbitMQ pela PaymentsAPI.", typeof(T).Name);
    }
}

/// <summary>
/// Consumidor do evento <see cref="OrderPlaceEvent"/> na PaymentsAPI.
/// Simula o processamento de pagamento e publica <see cref="PaymentProcessedEvent"/> (Approved ou Rejected).
/// </summary>
public class OrderPlaceConsumer : IConsumer<OrderPlaceEvent>
{
    private readonly IPaymentService _paymentService;
    private readonly ILogger<OrderPlaceConsumer> _logger;

    public OrderPlaceConsumer(
        IPaymentService paymentService,
        ILogger<OrderPlaceConsumer> logger)
    {
        _paymentService = paymentService;
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<OrderPlaceEvent> context)
    {
        var message = context.Message;
        _logger.LogInformation(
            "[PaymentsAPI] Consumindo OrderPlaceEvent da fila MQ: OrderId={OrderId}, UserId={UserId}, GameId={GameId}, Price={Price}",
            message.OrderId, message.UserId, message.GameId, message.Price);

        await _paymentService.ProcessOrderPaymentAsync(message, context.CancellationToken);
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RabbitMqOptions>(configuration.GetSection(RabbitMqOptions.SectionName));

        var connectionString = configuration.GetConnectionString("DefaultConnection") ?? "Data Source=payments.db";
        services.AddDbContext<PaymentsDbContext>(options =>
            options.UseSqlite(connectionString));

        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IEventPublisher, MassTransitEventPublisher>();

        var rabbitOptions = configuration.GetSection(RabbitMqOptions.SectionName).Get<RabbitMqOptions>() ?? new RabbitMqOptions();

        services.AddMassTransit(x =>
        {
            x.AddConsumer<OrderPlaceConsumer>();

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(rabbitOptions.Host, rabbitOptions.Port, rabbitOptions.VirtualHost, h =>
                {
                    h.Username(rabbitOptions.Username);
                    h.Password(rabbitOptions.Password);
                });

                cfg.ReceiveEndpoint(rabbitOptions.Queues.OrderPlace, e =>
                {
                    e.ConfigureConsumer<OrderPlaceConsumer>(context);
                });
            });
        });

        return services;
    }
}

