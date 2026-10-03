using AspNetCore.Identity.MongoDbCore.Models;
using MongoDbGenericRepository.Attributes;

[CollectionName("Roles")]
public class ApplicationRole : MongoIdentityRole<Guid>
{
}