﻿using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Mond.Binding;
using Mond.Libraries.Async;

namespace Mond.Libraries
{
    /// <summary>
    /// Contains all of the async related libraries.
    /// </summary>
    public class AsyncLibraries : IMondLibraryCollection
    {
        public IEnumerable<IMondLibrary> Create(MondState state)
        {
            yield return new AsyncLibrary();
        }
    }

    /// <summary>
    /// Library containing the <c>Async</c>, <c>Task</c>, <c>TaskCompletionSource</c>,
    /// <c>CancellationTokenSource</c>, and <c>CancellationToken</c>.
    /// </summary>
    public class AsyncLibrary : IMondLibrary
    {
        public IEnumerable<KeyValuePair<string, MondValue>> GetDefinitions()
        {
            var asyncClass = AsyncClass.Create();
            yield return new KeyValuePair<string, MondValue>("Async", asyncClass);

            var taskModule = MondModuleBinder.Bind<TaskModule>();
            yield return new KeyValuePair<string, MondValue>("Task", taskModule);

            var tcsClass = MondClassBinder.Bind<TaskCompletionSourceClass>();
            yield return new KeyValuePair<string, MondValue>("TaskCompletionSource", tcsClass);

            var ctsClass = MondClassBinder.Bind<CancellationTokenSourceClass>();
            yield return new KeyValuePair<string, MondValue>("CancellationTokenSource", ctsClass);

            var ctClass = MondClassBinder.Bind<CancellationTokenClass>();
            yield return new KeyValuePair<string, MondValue>("CancellationToken", ctClass);
        }
    }

    public static class AsyncUtil
    {
        /// <summary>
        /// Runs a Mond sequence as an async function.
        /// Should only be used when implementing your own async methods.
        /// </summary>
        public static async Task<MondValue> RunMondTask(MondState state, MondValue enumerator)
        {
            var input = MondValue.Undefined;

            while (true)
            {
                var yielded = state.Call(enumerator["moveNext"], input);
                var result = enumerator["current"];

                if (!yielded)
                    return result;

                if (result.Type != MondValueType.Object)
                    throw new MondRuntimeException("Tasks may only yield objects");

                var task = result.UserData as Task<MondValue>;
                if (task != null)
                {
                    input = await task;
                    continue;
                }

                var getEnumerator = result["getEnumerator"];
                
                if (getEnumerator.Type != MondValueType.Function)
                    throw new MondRuntimeException("Task objects must define getEnumerator");

                var resultEnumerator = state.Call(getEnumerator);
                input = await RunMondTask(state, resultEnumerator);
            }
        }

        /// <summary>
        /// Converts a Task to a MondValue.
        /// </summary>
        public static MondValue ToObject(Task task)
        {
            return ToObject(task.ContinueWith(t => MondValue.Undefined));
        }

        /// <summary>
        /// Converts a Task to a MondValue.
        /// </summary>
        public static MondValue ToObject(Task<MondValue> task)
        {
            return new MondValue(MondValueType.Object)
            {
                Prototype = MondValue.Null,
                UserData = task
            };
        }

        /// <summary>
        /// Converts an array of MondValues to an array of Tasks.
        /// </summary>
        public static Task<MondValue>[] ToTaskArray(MondState state, params MondValue[] tasks)
        {
            if (tasks.Length == 1 && tasks[0].Type == MondValueType.Array)
                tasks = tasks[0].ArrayValue.ToArray();

            return tasks
                .Select(t =>
                {
                    var task = t.UserData as Task<MondValue>;
                    if (task != null)
                        return task;

                    return RunMondTask(state, t);
                })
                .ToArray();
        }

        /// <summary>
        /// Tries to convert a MondValue to a CancellationToken.
        /// </summary>
        public static CancellationToken? AsCancellationToken(MondValue value)
        {
            if (value.Type != MondValueType.Object)
                return null;

            var token = value.UserData as CancellationTokenClass;

            if (token == null)
                return null;

            return token.CancellationToken;
        }
    }
}