// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Plugins.Tools
{
    /// <summary>
    /// A generic type converter that enables polymorphic deserialization based on a 'type' field.
    /// </summary>
    /// <typeparam name="TBase">The base class or interface for the polymorphic types.</typeparam>
    public class PolymorphicTypeConverter<TBase> : IYamlTypeConverter where TBase : class
    {
        private readonly Dictionary<string, Type> _typeMappings;

        /// <summary>
        /// Initializes the converter with a mapping from type names to C# types.
        /// </summary>
        /// <param name="typeMappings">A dictionary where keys are the values from the 'type' field in YAML
        /// and values are the corresponding concrete C# Types.</param>
        public PolymorphicTypeConverter(Dictionary<string, Type> typeMappings)
        {
            _typeMappings = typeMappings;
        }

        public bool Accepts(Type type) => type == typeof(TBase);

        public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        {
            // We need to deserialize the current node twice:
            // 1. First, as a generic object to inspect the 'type' field.
            // 2. Second, into the specific concrete type identified in step 1.

            // To do this, we deserialize into a YamlNode representation first.
            var deserializer = new DeserializerBuilder().Build();
            var node = deserializer.Deserialize<YamlDotNet.RepresentationModel.YamlMappingNode>(parser);

            // Find the mapping for the 'type' property.
            if (!node.Children.TryGetValue(new YamlDotNet.RepresentationModel.YamlScalarNode("type"), out var typeNode))
            {
                throw new InvalidOperationException($"Missing 'type' property for polymorphic type '{typeof(TBase).Name}'.");
            }

            var typeName = ((YamlDotNet.RepresentationModel.YamlScalarNode)typeNode).Value;
            if (typeName == null || !_typeMappings.TryGetValue(typeName, out var concreteType))
            {
                throw new InvalidOperationException($"Type '{typeName}' is not a registered subtype for '{typeof(TBase).Name}'.");
            }

            // Now that we have the concrete type, deserialize the node into it.
            // We need a new serializer that does NOT have this type converter,
            // or we'll get into an infinite loop.
            var concreteDeserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance) // Use Null convention as we rely on YamlMember attributes
                .Build();

            // 1. Serialize the node back to a string in memory.
            var stringSerializer = new SerializerBuilder().Build();
            var yamlString = stringSerializer.Serialize(node);

            // 2. Build a new deserializer that does NOT have this polymorphic converter
            //    to avoid an infinite recursive loop.
            var concreteDeserializer2 = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance) // Match your model's attributes
                .Build();

            // 3. Deserialize the YAML string into the final concrete type.
            var result = concreteDeserializer2.Deserialize(yamlString, concreteType);
            if (result is null)
            {
                throw new InvalidOperationException($"Deserialization of type '{concreteType.Name}' resulted in null.");
            }
            return result;
        }

        public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
        {
            throw new NotImplementedException();
        }
    }
}


