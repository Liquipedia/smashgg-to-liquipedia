using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace smashgg_to_liquipedia
{
    public static class JsonExtensions
    {
        /// <summary>
        /// Serializer options shared by all JSON reads/writes.
        /// <para>Public fields are included and numbers are accepted for string members (smash.gg IDs can be either).</para>
        /// </summary>
        public static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            IncludeFields = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            Converters = { new JsonStringEnumConverter(), new LenientStringConverter() }
        };

        /// <summary>
        /// Reads JSON numbers into string members as their textual representation
        /// </summary>
        private class LenientStringConverter : JsonConverter<string>
        {
            public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                if (reader.TokenType == JsonTokenType.Number)
                {
                    if (reader.TryGetInt64(out long value))
                    {
                        return value.ToString(CultureInfo.InvariantCulture);
                    }
                    return reader.GetDouble().ToString(CultureInfo.InvariantCulture);
                }

                return reader.GetString();
            }

            public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value);
            }
        }
    }
}
