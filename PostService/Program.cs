using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json.Linq;
using PostService.Data;
using PostService.Entities;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<PostServiceContext>(options =>
         options.UseSqlite(@"Data Source=user.db"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<PostServiceContext>();
        dbContext.Database.EnsureCreated();
    }

    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();

app.MapControllers();

ListenForIntegrationEvents(app);

app.Run();

static void ListenForIntegrationEvents(IHost host)
{
    var factory = new ConnectionFactory()
    {
        HostName = "localhost", // Docker host
        Port = 5672, // Default RabbitMQ port
        UserName = "guest", // Default RabbitMQ username
        Password = "guest", // Default RabbitMQ password
        VirtualHost = "/" // Default virtual host
    };
    var connection = factory.CreateConnection();
    var channel = connection.CreateModel();
    var consumer = new EventingBasicConsumer(channel);

    consumer.Received += (model, ea) =>
    {
        var body = ea.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        Console.WriteLine(" [x] Received {0}", message);
        var data = JObject.Parse(message);
        var type = ea.RoutingKey;

        using var localScope = host.Services.CreateScope();
        var localDbContext = localScope.ServiceProvider.GetRequiredService<PostServiceContext>();

        if (type == "user.add")
        {
            localDbContext.User.Add(new User()
            {
                ID = data["id"].Value<int>(),
                Name = data["name"].Value<string>()
            });
            localDbContext.SaveChanges();
        }
        else if (type == "user.update")
        {
            var user = localDbContext.User.First(a => a.ID == data["id"].Value<int>());
            user.Name = data["newname"].Value<string>();
            localDbContext.SaveChanges();
        }
    };
    var queueName = "user.postservice";
    channel.QueueDeclare(queue: queueName,
                         durable: true,  // Ensure the queue is durable if needed
                         exclusive: false,
                         autoDelete: false,
                         arguments: null);

    channel.BasicConsume(queue: "user.postservice",
                         autoAck: true,
                         consumer: consumer);
}