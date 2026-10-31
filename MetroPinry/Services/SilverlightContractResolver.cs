using System;
using System.Collections;
using Newtonsoft.Json.Serialization;

namespace MetroPinry.Services
{
    /// <summary>
    /// Резолвер контрактов для Silverlight CoreCLR, полностью исключающий
    /// вызов SecurityCritical-методов System.ComponentModel.TypeConverter.
    /// </summary>
    public class SilverlightContractResolver : DefaultContractResolver
    {
        public static readonly SilverlightContractResolver Instance = new SilverlightContractResolver();

        protected override JsonContract CreateContract(Type objectType)
        {
            Type underlyingType = Nullable.GetUnderlyingType(objectType) ?? objectType;

            // 1. Примитивы, перечисления, строки, числа, даты, URI
            if (underlyingType.IsPrimitive ||
                underlyingType.IsEnum ||
                underlyingType == typeof(string) ||
                underlyingType == typeof(decimal) ||
                underlyingType == typeof(DateTime) ||
                underlyingType == typeof(DateTimeOffset) ||
                underlyingType == typeof(TimeSpan) ||
                underlyingType == typeof(Guid) ||
                underlyingType == typeof(Uri))
            {
                return base.CreatePrimitiveContract(objectType);
            }

            // 2. Словари
            if (typeof(IDictionary).IsAssignableFrom(objectType))
            {
                return base.CreateDictionaryContract(objectType);
            }

            // 3. Коллекции, списки и массивы
            if (typeof(IEnumerable).IsAssignableFrom(objectType))
            {
                return base.CreateArrayContract(objectType);
            }

            // 4. Обычные пользовательские классы и модели
            return base.CreateObjectContract(objectType);
        }
    }
}