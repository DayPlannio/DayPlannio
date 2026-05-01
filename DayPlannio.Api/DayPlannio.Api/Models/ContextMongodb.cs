using MongoDB.Driver;

namespace DayPlannio.Api.Models
{
    public class ContextMongodb
    {
        public static string? ConnectionString { get; set; }
        public static string? Database { get; set; }
        public static bool IsSSL { get; set; }
        private IMongoDatabase _database { get; }

        
        public ContextMongodb()
        {
            try 
            {
                MongoClientSettings settings = MongoClientSettings.FromUrl(new MongoUrl(ConnectionString));
                if(IsSSL)
                {
                    settings.SslSettings = new SslSettings { EnabledSslProtocols = System.Security.Authentication.SslProtocols.Tls12 };
                }
                var mongoCliente = new MongoClient(settings);
                _database = mongoCliente.GetDatabase(Database);

            } 
            catch(Exception)
            {
                throw new Exception("Não foi possível conectar Mongodb");
            }

        }

        public IMongoCollection<ApplicationUser> ApplicationUsers
        {
            get
            {
                return _database.GetCollection<ApplicationUser>("Users");
            }
        }

        public IMongoCollection<User> User
        {
            get
            {
                return _database.GetCollection<User>("User");
            }
        }

        public IMongoCollection<Cliente> Cliente
        {
            get
            {
                return _database.GetCollection<Cliente>("Cliente");
            }
        }

        public IMongoCollection<TipoServico> TipoServico
        {
            get
            {
                return _database.GetCollection<TipoServico>("TipoServico");
            }
        }

        public IMongoCollection<Agendamento> Agendamento
        {
            get
            {
                return _database.GetCollection<Agendamento>("Agendamento");
            }
        }

        public IMongoCollection<Financeiro> Financeiro
        {
            get
            {
                return _database.GetCollection<Financeiro>("Financeiro");
            }
        }
    }
}
