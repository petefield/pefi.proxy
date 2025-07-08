using Microsoft.Extensions.Options;
using pefi.Rabbit;

namespace pefi
{
    public static class ServiceCollectionExtensions
    {


        public class MessagingConfig()
        {
            public string Address { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }

        }
        public static IServiceCollection AddPeFiMessaging(this IServiceCollection services, Action<MessagingConfig> configureOptions)
        {
            services.Configure(configureOptions);

            return services.AddSingleton<IMessageBroker>(sp => {

                var options = sp.GetRequiredService<IOptions<MessagingConfig>>().Value;
                return new MessageBroker(options.Address, options.Username, options.Password);

            } );

        }
        public static IServiceCollection AddPeFiMessaging(this IServiceCollection services, string address, string username, string password)
            => services.AddSingleton<IMessageBroker>(sp => new MessageBroker(address, username, password));


    }

}
