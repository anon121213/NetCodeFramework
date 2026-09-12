using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using QuaternionFormatter = Skynet.FormatterSystem.Formatters.QuaternionFormatter;
using Vector3Formatter = Skynet.FormatterSystem.Formatters.Vector3Formatter;

namespace Skynet.FormatterSystem
{
    public class NetworkFormatter : INetworkFormatter
    {
        public void Initialize()
        {
            var formatters = new IMessagePackFormatter[]
            {
                new Vector3Formatter(),
                new QuaternionFormatter(),
            };

            var resolvers = new IFormatterResolver[]
            {
                StandardResolver.Instance 
            };
            
            var options = MessagePackSerializerOptions.Standard
                .WithResolver(CompositeResolver.Create(formatters, resolvers));

            MessagePackSerializer.DefaultOptions = options;
        }
    }

    public interface INetworkFormatter
    {
        void Initialize();
    }
}