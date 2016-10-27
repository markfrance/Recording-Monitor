using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.Remoting.Messaging;
using System.Threading;

namespace System
{
    partial class MSharpExtensions
    {
        static ConcurrentDictionary<Tuple<Type, Type>, bool> TypeImplementsCache = new ConcurrentDictionary<Tuple<Type, Type>, bool>();

        /// <summary>
        /// Gets all parent types hierarchy for this type.
        /// </summary>
        public static IEnumerable<Type> GetParentTypes(this Type type)
        {
            var result = new List<Type>();

            for (var @base = type.BaseType; @base != null; @base = @base.BaseType)
                result.Add(@base);

            return result;
        }

        /// <summary>
        /// Determines whether this type inherits from a specified base type, either directly or indirectly.
        /// </summary>
        public static bool InhritsFrom(this Type type, Type baseType)
        {
            if (baseType == null)
                throw new ArgumentNullException("baseType");

            if (baseType.IsInterface)
                return type.Implements(baseType);

            return type.GetParentTypes().Contains(baseType);
        }

        public static bool Implements<T>(this Type type)
        {
            return Implements(type, typeof(T));
        }

        public static bool IsA<T>(this Type type)
        {
            return typeof(T).IsAssignableFrom(type);
        }

        public static bool IsA(this Type thisType, Type type)
        {
            return type.IsAssignableFrom(thisType);
        }

        public static bool References(this Assembly assembly, Assembly anotherAssembly)
        {
            if (assembly == null) throw new NullReferenceException();
            if (anotherAssembly == null) throw new ArgumentNullException("anotherAssembly");

            return assembly.GetReferencedAssemblies().Any(each => each.FullName.Equals(anotherAssembly.FullName));
        }

        public static string GetDisplayName(this Type input)
        {
            string displayName = input.Name;

            for (int i = displayName.Length - 1; i >= 0; i--)
            {
                if (displayName[i] == char.ToUpper(displayName[i]))
                    if (i > 0)
                        displayName = displayName.Insert(i, " ");
            }

            return displayName;
        }

        public static bool Implements(this Type type, Type interfaceType)
        {
            if (interfaceType == null) throw new ArgumentNullException("interfaceType");

            var key = Tuple.Create(type, interfaceType);

            return TypeImplementsCache.GetOrAdd(key, t =>
            {
                if (!interfaceType.IsInterface)
                    throw new ArgumentException("The provided value for interfaceType, " + interfaceType.FullName + " is not an interface type.");

                if (t.Item1 == t.Item2) return true;

                var implementedInterface = t.Item1.GetInterface(t.Item2.FullName, ignoreCase: false);

                if (implementedInterface == null) return false;
                else return implementedInterface.FullName == t.Item2.FullName;
            });
        }

        /// <summary>
        /// Gets the value of this property on the specified object.
        /// </summary>
        public static object GetValue(this PropertyInfo property, object @object)
        {
            try
            {
                return property.GetValue(@object, null);
            }
            catch (Exception ex)
            {
                throw new Exception("Could not get the value of property '{0}.{1}' on the specified instance: {2}".FormatWith(property.DeclaringType.Name, property.Name, ex.Message), ex);
            }
        }

        /// <summary>
        /// Set the value of this property on the specified object.
        /// </summary>
        public static void SetValue(this PropertyInfo property, object @object, object value)
        {
            property.SetValue(@object, value, null);
        }

        /// <summary>
        /// Adds the specified types pair to this type dictionary.
        /// </summary>
        public static void Add<T, K>(this IDictionary<Type, Type> typeDictionary)
        {
            typeDictionary.Add(typeof(T), typeof(K));
        }

        /// <summary>
        /// Creates the instance of this type.
        /// </summary>
        public static object CreateInstance(this Type type, params object[] constructorParameters)
        {
            return Activator.CreateInstance(type, constructorParameters);
        }

        /// <summary>
        /// Determines whether it has a specified attribute applied to it.
        /// </summary>
        public static bool Defines<TAttribute>(this MemberInfo member, bool inherit = true) where TAttribute : Attribute
        {
            return member.IsDefined(typeof(TAttribute), inherit);
        }

        /// <summary>
        /// Creates the instance of this type casted to the specified type.
        /// </summary>
        public static TCast CreateInstance<TCast>(this Type type, params object[] constructorParameters)
        {
            return (TCast)Activator.CreateInstance(type, constructorParameters);
        }

        public static T Cache<T>(this MethodBase method, object[] arguments, Method<T> methodBody) where T : class
        {
            return Cache<T>(method, null, arguments, methodBody);
        }

        /// <summary>
        /// Determines if this type is a nullable of something.
        /// </summary>
        public static bool IsNullable(this Type type)
        {
            if (!type.IsGenericType) return false;

            if (type.GetGenericTypeDefinition() != typeof(Nullable<>)) return false;

            return true;
        }

        public delegate T Method<out T>();

        public static T Cache<T>(this MethodBase method, object instance, object[] arguments, Method<T> methodBody) where T : class
        {
            var key = method.DeclaringType.GUID + ":" + method.Name;
            if (instance != null)
                key += instance.GetHashCode() + ":";

            arguments?.Do(arg => key += arg.GetHashCode() + ":");

            if (CallContext.GetData(key) == null)
            {
                var result = methodBody?.Invoke();
                CallContext.SetData(key, result);
                return result;
            }

            return CallContext.GetData(key) as T;
        }

        public static bool Is<T>(this PropertyInfo property, string propertyName)
        {
            var type1 = property.DeclaringType;
            var type2 = typeof(T);

            if (type1.IsA(type2) || type2.IsA(type1))
            {
                return property.Name == propertyName;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Determines whether this property info is the specified property (in lambda expression).
        /// </summary>
        public static bool Is<T>(this PropertyInfo property, Expression<Func<T, object>> expression)
        {
            if (!typeof(T).IsA(property.DeclaringType)) return false;

            var body = expression.Body;

            string memberName;

            if (body is UnaryExpression)
            {
                body = (body as UnaryExpression).Operand;

                //if (!(body as UnaryExpression).Expression.Type.IsA(typeof(T))) return false;
                //memberName = ((body as UnaryExpression).Operand as MemberExpression).Member.Name;
            }

            if (body is MemberExpression)
            {
                //if (!(body as MemberExpression).Expression.Type.IsA(typeof(T))) return false;
                memberName = (body as MemberExpression).Member.Name;
            }
            else throw new NotSupportedException(body.GetType() + " is not supported.");

            return property.Name == memberName;
        }

        /// <summary>
        /// Determines whether this type is static.
        /// </summary>
        public static bool IsStatic(this Type type)
        {
            return type.IsAbstract && type.IsSealed;
        }

        public static bool IsExtensionMethod(this MethodInfo method)
        {
            return method.GetCustomAttributes<Runtime.CompilerServices.ExtensionAttribute>(inherit: false).Any();
        }

        ///// <summary>
        ///// Gets all defined attributes of the specified type.
        ///// </summary>
        //public static TAttribute[] GetCustomAttributes1<TAttribute>(this MemberInfo member, bool inherit = true) where TAttribute : Attribute
        //{
        //    member.GetCustomAttributes<TAttribute>()

        //    var result = member.GetCustomAttributes(typeof(TAttribute), inherit);
        //    if (result == null) return new TAttribute[0];
        //    else return result.Cast<TAttribute>().ToArray();
        //}

        #region Sub-Types

        static ConcurrentDictionary<Assembly, ConcurrentDictionary<Type, IEnumerable<Type>>> SubTypesCache = new ConcurrentDictionary<Assembly, ConcurrentDictionary<Type, IEnumerable<Type>>>();

        /// <summary>
        /// Gets all types in this assembly that are directly inherited from a specified base type.
        /// </summary>
        public static IEnumerable<Type> GetSubTypes(this Assembly assembly, Type baseType)
        {
            var cache = SubTypesCache.GetOrAdd(assembly, a => new ConcurrentDictionary<Type, IEnumerable<Type>>());

            //if (!SubTypesCache.ContainsKey(assembly))
            //    lock (SubTypesCache)
            //    {
            //        if (!SubTypesCache.ContainsKey(assembly))
            //            SubTypesCache.Add(assembly, new Dictionary<Type, IEnumerable<Type>>());
            //    }

            //var cache = SubTypesCache[assembly];

            return cache.GetOrAdd(baseType, bt =>
            {
                try
                {
                    return assembly.GetTypes().Where(t => t.BaseType == bt).ToArray();
                }
                catch (ReflectionTypeLoadException ex)
                {
                    throw new Exception("Could not load the types of the assembly '{0}'. Type-load exceptions: {1}".FormatWith(assembly.FullName,
                        ex.LoaderExceptions.Select(e => e.Message).Distinct().ToString(" | ")));
                }
            });

            //if (!cache.ContainsKey(baseType))
            //    lock (assembly)
            //    {
            //        if (!cache.ContainsKey(baseType))
            //        {
            //            Type[] subTypes;
            //            try
            //            {
            //                subTypes = assembly.GetTypes().Where(t => t.BaseType == baseType).ToArray();
            //            }
            //            catch (ReflectionTypeLoadException ex)
            //            {
            //                throw new Exception("Could not load the types of the assembly '{0}'. Type-load exceptions: {1}".FormatWith(assembly.FullName,
            //                    ex.LoaderExceptions.Select(e => e.Message).Distinct().ToString(" | ")));
            //            }

            //            cache.Add(baseType, subTypes);
            //        }
            //    }

            //return cache[baseType];
        }

        #endregion

        static ConcurrentDictionary<Type, string> ProgrammingNameCache = new ConcurrentDictionary<Type, string>();

        /// <summary>
        /// Gets the full programming name of this type. Unlike the standard FullName property, it handles Generic types properly.
        /// </summary>
        public static string GetProgrammingName(this Type type)
        {
            return ProgrammingNameCache.GetOrAdd(type, x => GetProgrammingName(x, useGlobal: false));
        }

        /// <summary>
        /// Gets the full programming name of this type. Unlike the standard FullName property, it handles Generic types properly.
        /// </summary>
        public static string GetProgrammingName(this Type type, bool useGlobal, bool useNamespace = true, bool useNamespaceForParams = true, bool useGlobalForParams = false)
        {
            if (type.GetGenericArguments().Any())
            {
                return "global::".OnlyWhen(useGlobal && type.FullName != null) +
                    "{0}{1}<{2}>".FormatWith(
                    type.Namespace.OnlyWhen(useNamespace).WithSuffix("."),
                    type.Name.Remove(type.Name.IndexOf('`')),
                    type.GetGenericArguments().Select(t => t.GetProgrammingName(useGlobalForParams, useNamespaceForParams, useNamespaceForParams, useGlobalForParams)).ToString(", "));
            }
            else
            {
                if (type.FullName == null)
                {
                    // Generic parameter name:
                    return type.Name.TrimEnd("&");
                }

                return "global::".OnlyWhen(useGlobal) + type.Namespace.OnlyWhen(useNamespace).WithSuffix(".") + type.Name.Replace("+", ".").TrimEnd("&");
            }
        }

        /// <summary>
        /// Determines if this type is a generic class  of the specified type.
        /// </summary>
        public static bool IsGenericOf(this Type type, Type genericType, params Type[] genericParameters)
        {
            if (!type.IsGenericType) return false;

            if (type.GetGenericTypeDefinition() != genericType) return false;

            var args = type.GetGenericArguments();

            if (args.Length != genericParameters.Length) return false;

            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] != genericParameters[i]) return false;
            }

            return true;
        }

        /// <summary>
        /// Gets the specified property.
        /// </summary>
        public static Expression<Func<T, object>> GetProperty<T>(this T instance, Expression<Func<T, object>> property)
        {
            return property;
        }

        /// <summary>
        /// Gets the specified property.
        /// </summary>
        public static string GetPropertyName<T>(this T instance, Expression<Func<T, object>> property)
        {
            return GetProperty<T>(instance, property).GetPropertyName();
        }

        /// <summary>
        /// Gets the property name for a specified expression.
        /// </summary>
        public static MemberInfo GetMember<T, K>(this Expression<Func<T, K>> memberExpression)
        {
            var asMemberExpression = memberExpression.Body as MemberExpression;

            if (asMemberExpression == null)
            {
                // Maybe Unary:
                asMemberExpression = (memberExpression.Body as UnaryExpression)?.Operand as MemberExpression;
            }

            if (asMemberExpression == null) throw new Exception("Invalid expression");

            return asMemberExpression.Member;
        }

        /// <summary>
        /// Gets the property name for a specified expression.
        /// </summary>
        public static string GetPropertyName<T>(this Expression<Func<T, object>> propertyExpression)
        {
            return propertyExpression.GetProperty<T, object>().Name;
        }

        static ConcurrentDictionary<Type, string> AssemblyQualifiedNameCache = new ConcurrentDictionary<Type, string>();
        public static string GetCachedAssemblyQualifiedName(this Type type)
        {
            return AssemblyQualifiedNameCache.GetOrAdd(type, x => x.AssemblyQualifiedName);
        }

        /// <summary>
        /// Gets the root entity type of this type.
        /// If this type inherits directly from Entity&lt;T&gt; then it will be returned, otherwise its parent...
        /// </summary>
        public static Type GetRootEntityType(this Type objectType)
        {
            if (objectType.BaseType == null)
                throw new NotSupportedException(objectType.FullName + " not recognised. It must be a subclass of MSharp.Framework.Entity.");

            if (objectType.BaseType.Name == "GuidEntity") return objectType;
            if (objectType.BaseType == typeof(MSharp.Framework.Entity<int>)) return objectType;
            if (objectType.BaseType == typeof(MSharp.Framework.Entity<long>)) return objectType;
            if (objectType.BaseType == typeof(MSharp.Framework.Entity<string>)) return objectType;

            return GetRootEntityType(objectType.BaseType);
        }

        /// <summary>
        /// Gets all types in the current appDomain which implement this interface.
        /// </summary>
        /// <param name="interfaceType"></param>
        /// <returns></returns>
        public static List<Type> FindImplementerClasses(this Type interfaceType)
        {
            if (!interfaceType.IsInterface) throw new InvalidOperationException(interfaceType.GetType().FullName + " is not an Interface.");

            var result = new List<Type>();

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies().Where(a => a.References(interfaceType.Assembly)))
            {
                try
                {
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type == interfaceType) continue;
                        if (!type.IsClass) continue;

                        if (type.Implements(interfaceType))
                        {
                            result.Add(type);
                        }
                    }
                }
                catch
                {
                    // Can't load assembly
                }
            }

            return result;
        }

        public static object GetObjectByPropertyPath(this Type type, object instance, string propertyPath)
        {
            if (propertyPath.Contains("."))
            {
                var directProperty = type.GetProperty(propertyPath.TrimAfter("."));

                if (directProperty == null)
                    throw new Exception(type.FullName + " does not have a property named '" + propertyPath.TrimAfter(".") + "'");

                var associatedObject = directProperty.GetValue(instance);
                if (associatedObject == null) return null;

                var remainingPath = propertyPath.TrimStart(directProperty.Name + ".");
                return associatedObject.GetType().GetObjectByPropertyPath(associatedObject, remainingPath);
            }
            else
            {
                return type.GetProperty(propertyPath).GetValue(instance);
            }
        }

        /// <summary>
        /// Creates a new thread and copies the current Culture and UI Culture.
        /// </summary>
        public static Thread CreateNew(this Thread thread, Action threadStart)
        {
            return CreateNew(thread, threadStart, null);
        }

        /// <summary>
        /// Creates a new thread and copies the current Culture and UI Culture.
        /// </summary>
        public static Thread CreateNew(this Thread thread, Action threadStart, Action<Thread> initializer)
        {
            var result = new Thread(new ThreadStart(threadStart))
            {
                CurrentCulture = thread.CurrentCulture,
                CurrentUICulture = thread.CurrentUICulture
            };

            initializer?.Invoke(result);

            return result;
        }

        public static PropertyInfo GetProperty<TModel, TProperty>(this Expression<Func<TModel, TProperty>> property)
        {
            return property.GetMember<TModel, TProperty>() as PropertyInfo;
        }

        /// <summary>
        /// Gets the default value for this type. It's equivalent to default(T).
        /// </summary>
        public static object GetDefaultValue(this Type type)
        {
            if (type.IsValueType)
                return Activator.CreateInstance(type);

            return null;
        }

        /// <summary>
        /// Determine whether this property is static.
        /// </summary>
        public static bool IsStatic(this PropertyInfo property)
        {
            return (property.GetGetMethod() ?? property.GetSetMethod()).IsStatic;
        }
    }
}
