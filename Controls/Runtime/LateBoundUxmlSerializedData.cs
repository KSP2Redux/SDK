using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Scripting;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace UitkForKsp2.Controls
{
    /// <summary>
    /// Defers construction of generated UXML serialized data until its declaring assembly is loaded.
    /// </summary>
    [Serializable]
    [Preserve]
    public sealed class LateBoundUxmlSerializedData : UxmlSerializedData
    {
        [Serializable]
        private sealed class ObjectFieldReference
        {
            [SerializeField] private string _declaringTypeName;
            [SerializeField] private string _fieldName;
            [SerializeField] private bool _isCollection;
            [SerializeField] private Object _value;
            [SerializeField] private List<Object> _values = new();

            public ObjectFieldReference(FieldInfo field, object fieldValue)
            {
                _declaringTypeName = field.DeclaringType?.FullName;
                _fieldName = field.Name;

                if (fieldValue is Object objectValue)
                {
                    _value = objectValue;
                    return;
                }

                if (fieldValue is not IEnumerable enumerable)
                {
                    return;
                }

                _isCollection = true;
                foreach (object item in enumerable)
                {
                    _values.Add(item as Object);
                }
            }

            public void Restore(object target)
            {
                Type declaringType = FindTypeInHierarchy(target.GetType(), _declaringTypeName);
                FieldInfo field = declaringType?.GetField(
                    _fieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly
                );
                if (field == null)
                {
                    throw new MissingFieldException(_declaringTypeName, _fieldName);
                }

                if (!_isCollection)
                {
                    field.SetValue(target, _value);
                    return;
                }

                Type elementType = GetCollectionElementType(field.FieldType);
                if (field.FieldType.IsArray)
                {
                    var array = Array.CreateInstance(elementType, _values.Count);
                    for (int index = 0; index < _values.Count; index++)
                    {
                        array.SetValue(_values[index], index);
                    }

                    field.SetValue(target, array);
                    return;
                }

                var list = field.GetValue(target) as IList;
                if (list == null)
                {
                    list = (IList)Activator.CreateInstance(field.FieldType, true);
                    field.SetValue(target, list);
                }

                list.Clear();
                foreach (Object item in _values)
                {
                    list.Add(item);
                }
            }
        }

        [SerializeField] private string _serializedDataTypeName;
        [SerializeField] private string _serializedDataJson;
        [SerializeField] private List<ObjectFieldReference> _objectFieldReferences = new();

        private LateBoundUxmlSerializedData()
        {
        }

        /// <summary>
        /// Creates a late-bound copy of generated UXML serialized data.
        /// </summary>
        /// <param name="source">The generated serialized data to copy.</param>
        /// <returns>A player-loadable proxy for the generated data.</returns>
        public static LateBoundUxmlSerializedData Create(UxmlSerializedData source)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            Type sourceType = source.GetType();
            var result = new LateBoundUxmlSerializedData
            {
                _serializedDataTypeName = GetStableTypeName(sourceType),
                _serializedDataJson = JsonUtility.ToJson(source)
            };

            foreach (FieldInfo field in GetSerializableFields(sourceType))
            {
                if (IsUnityObjectField(field.FieldType))
                {
                    result._objectFieldReferences.Add(new ObjectFieldReference(field, field.GetValue(source)));
                }
            }

            return result;
        }

        /// <inheritdoc />
        public override object CreateInstance()
        {
            return CreateSerializedData().CreateInstance();
        }

        /// <inheritdoc />
        public override void Deserialize(object obj)
        {
            CreateSerializedData().Deserialize(obj);
        }

        private UxmlSerializedData CreateSerializedData()
        {
            Type serializedDataType = ResolveType(_serializedDataTypeName);
            if (serializedDataType == null)
            {
                throw new TypeLoadException(
                    $"Could not resolve late-loaded UXML serialized data type '{_serializedDataTypeName}'. " +
                    "Ensure the mod assembly is loaded before its VisualTreeAsset is instantiated."
                );
            }

            if (!typeof(UxmlSerializedData).IsAssignableFrom(serializedDataType))
            {
                throw new InvalidOperationException(
                    $"Late-loaded UXML type '{_serializedDataTypeName}' does not derive from UxmlSerializedData."
                );
            }

            var serializedData = (UxmlSerializedData)Activator.CreateInstance(serializedDataType, true);
            JsonUtility.FromJsonOverwrite(_serializedDataJson, serializedData);
            foreach (ObjectFieldReference objectFieldReference in _objectFieldReferences)
            {
                objectFieldReference.Restore(serializedData);
            }

            return serializedData;
        }

        private static string GetStableTypeName(Type type)
        {
            return $"{type.FullName}, {type.Assembly.GetName().Name}";
        }

        private static Type ResolveType(string stableTypeName)
        {
            var resolvedType = Type.GetType(stableTypeName, false);
            if (resolvedType != null)
            {
                return resolvedType;
            }

            int separatorIndex = stableTypeName.LastIndexOf(',');
            if (separatorIndex < 0)
            {
                return null;
            }

            string typeName = stableTypeName.Substring(0, separatorIndex).Trim();
            string assemblyName = stableTypeName.Substring(separatorIndex + 1).Trim();
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                if (string.Equals(assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase))
                {
                    return assembly.GetType(typeName, false);
                }
            }

            return null;
        }

        private static IEnumerable<FieldInfo> GetSerializableFields(Type type)
        {
            for (Type current = type; current != null && current != typeof(UxmlSerializedData); current = current.BaseType)
            {
                foreach (FieldInfo field in current.GetFields(
                             BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly
                         ))
                {
                    if (field.IsStatic || field.IsNotSerialized)
                    {
                        continue;
                    }

                    if (field.IsPublic || field.GetCustomAttribute<SerializeField>() != null)
                    {
                        yield return field;
                    }
                }
            }
        }

        private static bool IsUnityObjectField(Type type)
        {
            if (typeof(Object).IsAssignableFrom(type))
            {
                return true;
            }

            Type elementType = GetCollectionElementType(type);
            return elementType != null && typeof(Object).IsAssignableFrom(elementType);
        }

        private static Type GetCollectionElementType(Type type)
        {
            if (type.IsArray)
            {
                return type.GetElementType();
            }

            if (type.IsGenericType && typeof(IList).IsAssignableFrom(type))
            {
                return type.GetGenericArguments()[0];
            }

            return null;
        }

        private static Type FindTypeInHierarchy(Type type, string fullName)
        {
            for (Type current = type; current != null; current = current.BaseType)
            {
                if (string.Equals(current.FullName, fullName, StringComparison.Ordinal))
                {
                    return current;
                }
            }

            return null;
        }
    }
}
