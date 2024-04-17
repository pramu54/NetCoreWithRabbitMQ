using RabbitMQ.Client;
using System.Text;
using UserService.Data;

namespace UserService
{
    public class IntegrationEventService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public IntegrationEventService(IServiceScopeFactory scopeFactory) 
        {
            _scopeFactory = scopeFactory;
            using var scope = _scopeFactory.CreateScope();
            using var dbContext = scope.ServiceProvider.GetRequiredService<UserServiceContext>();
            dbContext.Database.EnsureCreated();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) 
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await PublishOutstandingIntegrationEvent(stoppingToken);
            }
        }

        private async Task PublishOutstandingIntegrationEvent(CancellationToken stoppingToken)
        {
            try
            {
                var factory = new ConnectionFactory();
                var connection = factory.CreateConnection();
                var channel = connection.CreateModel();

                while (!stoppingToken.IsCancellationRequested)
                {
                    {
                        using var scope = _scopeFactory.CreateScope();
                        using var dbContext = scope.ServiceProvider.GetRequiredService<UserServiceContext>();
                        var events = dbContext.IntegrationEventOutbox.OrderBy(options => options.Id).ToList();
                        foreach (var e in events)
                        {
                            var body = Encoding.UTF8.GetBytes(e.Data);
                            channel.BasicPublish(exchange: "user",
                                                            routingKey: e.Event,
                                                            basicProperties: null,
                                                            body: body);
                            Console.WriteLine("Published: " + e.Event + " " + e.Data);
                            dbContext.Remove(e);
                            dbContext.SaveChanges();
                        }
                    }
                    await Task.Delay(1000, stoppingToken);
                }
            } catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                await Task.Delay(5000, stoppingToken);
            }
        }
    }
}
