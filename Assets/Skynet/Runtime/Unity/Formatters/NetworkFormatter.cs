using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;

namespace Skynet.Unity.Formatters
{
    public class NetworkFormatter : INetworkFormatter
    {
        public void Initialize()
        {
            var formatters = new IMessagePackFormatter[]
            {
                new Vector3Formatter(),
                new QuaternionFormatter(),
                new TransformSnapshotFormatter(),
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
}