using AspNetCore.Identity.MongoDbCore.Models;
using MongoDbGenericRepository.Attributes;

// Classe necessária para satisfazer a constraint do MongoDbCore.
// Não possui propriedades adicionais além das herdadas de MongoIdentityRole.
[CollectionName("Roles")]
public class ApplicationRole : MongoIdentityRole<Guid>
{
}