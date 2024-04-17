namespace UserService.Entities
{
    public class IntegrationEvent
    {
        public int Id { get; set; }
        public required string Event { get; set; }
        public required string Data { get; set; }
    }
}
