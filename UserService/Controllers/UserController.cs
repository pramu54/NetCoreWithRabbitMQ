using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System.Text;
using UserService.Data;
using UserService.Entities;

namespace UserService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly UserServiceContext _context;

        public UserController(UserServiceContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<User>>> GetUser()
        {
            return await _context.User.ToListAsync();
        }

        private void PublishToMessageQueue(string integrationEvent, string eventData)
        {
            // TOOO: Reuse and close connections and channel, etc, 
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
            var body = Encoding.UTF8.GetBytes(eventData);
            channel.BasicPublish(exchange: "User",
                                             routingKey: integrationEvent,
                                             basicProperties: null,
                                             body: body);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> PutUser([FromRoute]int id, [FromBody] User user)
        {   using var transaction = _context.Database.BeginTransaction();

            try {
                _context.Entry(user).State = EntityState.Modified;
                await _context.SaveChangesAsync();

                var integrationEventData = JsonConvert.SerializeObject(new
                {
                    id = user.ID,
                    newname = user.Name
                });
                _context.IntegrationEventOutbox.Add(
                    new IntegrationEvent() {
                        Event = "user.update",
                        Data = integrationEventData
                    });

                _context.SaveChanges();
                transaction.Commit();
                
            } catch (DbUpdateConcurrencyException ex) {
                //The code from Microsoft - Resolving concurrency conflicts 
                foreach (var entry in ex.Entries)
                {
                    if (entry.Entity is User)
                    {
                        var proposedValues = entry.CurrentValues; //Your proposed changes
                        var databaseValues = entry.GetDatabaseValues(); //Values in the Db

                        foreach (var property in proposedValues.Properties)
                        {
                            var proposedValue = proposedValues[property];
                            var databaseValue = databaseValues[property];

                            // TODO: decide which value should be written to database
                            // proposedValues[property] = <value to be saved>;
                        }

                        // Refresh original values to bypass next concurrency check
                        entry.OriginalValues.SetValues(databaseValues);
                    }
                    else
                    {
                        throw new NotSupportedException(
                            "Don't know how to handle concurrency conflicts for "
                            + entry.Metadata.Name);
                    }
                }
            }

            return NoContent();
        }

        [HttpPost]
        public async Task<ActionResult<User>> PostUser(User user)
        {
            using var transaction = _context.Database.BeginTransaction();

            _context.User.Add(user);
            _context.SaveChanges();

            var integrationEventData = JsonConvert.SerializeObject(new
            {
                id = user.ID,
                name = user.Name
            });
            _context.IntegrationEventOutbox.Add(
                    new IntegrationEvent()
                    {
                        Event = "user.add",
                        Data = integrationEventData
                    });

            _context.SaveChanges();
            transaction.Commit();

            return CreatedAtAction("GetUser", new { id = user.ID }, user);
        }

    }
}
