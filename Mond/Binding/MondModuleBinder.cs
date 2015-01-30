﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Mond.VirtualMachine.Prototypes;

namespace Mond.Binding
{
    public static class MondModuleBinder
    {
        private class ModuleBinding
        {
            public readonly Dictionary<string, MondFunction> Functions;

            public ModuleBinding(Dictionary<string, MondFunction> functions)
            {
                Functions = functions;
            }
        }

        private static ConcurrentDictionary<Type, ModuleBinding> _cache = new ConcurrentDictionary<Type, ModuleBinding>();

        /// <summary>
        /// Generates module bindings for T. Returns an object containing the bound methods.
        /// </summary>
        /// <param name="state">Optional state to bind to. Only required if you plan on using metamethods.</param>
        public static MondValue Bind<T>(MondState state = null)
        {
            return Bind(typeof(T), state);
        }

        /// <summary>
        /// Generates module bindings for a type. Returns an object containing the bound methods.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="state">Optional state to bind to. Only required if you plan on using metamethods.</param>
        public static MondValue Bind(Type type, MondState state = null)
        {
            return CopyToObject(BindImpl(type), state);
        }

        /// <summary>
        /// Generates module bindings for T. Returns a dictionary containing the bindings.
        /// </summary>
        public static ReadOnlyDictionary<string, MondFunction> BindFunctions<T>()
        {
            return new ReadOnlyDictionary<string, MondFunction>(BindImpl(typeof(T)));
        }

        /// <summary>
        /// Generates module bindings for a type. Returns a dictionary containing the bindings.
        /// </summary>
        public static ReadOnlyDictionary<string, MondFunction> BindFunctions(Type type)
        {
            return new ReadOnlyDictionary<string, MondFunction>(BindImpl(type));
        }

        private static MondValue CopyToObject(Dictionary<string, MondFunction> functions, MondState state)
        {
            var obj = new MondValue(state);
            obj.Prototype = MondValue.Null;

            foreach (var func in functions)
            {
                obj[func.Key] = func.Value;
            }

            obj.Prototype = ValuePrototype.Value;
            return obj;
        }

        private static Dictionary<string, MondFunction> BindImpl(Type type)
        {
            ModuleBinding binding;
            if (_cache.TryGetValue(type, out binding))
            {
                return binding.Functions;
            }

            var moduleAttrib = type.Attribute<MondModuleAttribute>();

            if (moduleAttrib == null)
                throw new MondBindingException(BindingError.TypeMissingAttribute, "MondModule");

            var moduleName = moduleAttrib.Name ?? type.Name;

            var result = new Dictionary<string, MondFunction>();

            var methods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);
            foreach (var method in MondFunctionBinder.BindStatic(moduleName, methods))
            {
                var name = method.Item1;

                if (result.ContainsKey(name))
                    throw new MondBindingException(BindingError.DuplicateDefinition, name);

                result[name] = method.Item2;
            }

            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Static);
            foreach (var property in properties.PropertyMethods())
            {
                var name = property.Item1;

                if (result.ContainsKey(name))
                    throw new MondBindingException(BindingError.DuplicateDefinition, name);

                var propertyArray = new[] { property.Item2 };

                var propertyBinding = MondFunctionBinder.BindStatic(moduleName, propertyArray, MondFunctionBinder.MethodType.Property, name)
                    .FirstOrDefault();

                if (propertyBinding != null)
                    result[name] = propertyBinding.Item2;
            }

            binding = new ModuleBinding(result);
            _cache.TryAdd(type, binding);

            return result;
        }
    }
}
