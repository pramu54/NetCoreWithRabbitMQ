using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.Text.Json.Serialization;

namespace UserService.Entities
{
    public class User
    {
        public int ID { get; set; }
        public required string Name { get; set;}
        public required string Mail { get; set;}
        public required string OtherData { get; set;}
    }
}
