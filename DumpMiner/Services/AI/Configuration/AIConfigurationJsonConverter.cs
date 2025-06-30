using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DumpMiner.Services.AI.Configuration
{
    /// <summary>
    /// Custom JSON converter for AIProviderType enum
    /// </summary>
    public class AIProviderTypeConverter : JsonConverter<AIProviderType>
    {
        public override AIProviderType Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.String)
            {
                var enumString = reader.GetString();
                if (Enum.TryParse<AIProviderType>(enumString, true, out var result))
                {
                    return result;
                }
            }
            
            // Default fallback
            return AIProviderType.OpenAI;
        }

        public override void Write(Utf8JsonWriter writer, AIProviderType value, JsonSerializerOptions options)
        {
            writer.WriteStringValue(value.ToString());
        }
    }
} 