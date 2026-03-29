#nullable enable
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Microsoft.CSharp;
using UnityCli.Protocol;
using UnityEngine;

namespace KinKeep.UnityCli.Bridge.Editor
{
    internal sealed class ExecuteCodeHandler
    {
        private const string WrapperTemplate = @"
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

public static class PucExecuteWrapper
{
    public static string Execute()
    {
        var __sb = new System.Text.StringBuilder();
        var __origOut = System.Console.Out;
        var __writer = new System.IO.StringWriter(__sb);
        System.Console.SetOut(__writer);
        try
        {
            /*__PUC_USER_CODE__*/
        }
        finally
        {
            System.Console.SetOut(__origOut);
        }

        return __sb.ToString();
    }
}";

        public bool CanHandle(string command)
        {
            return string.Equals(command, ProtocolConstants.CommandExecuteCode, StringComparison.Ordinal);
        }

        public string Handle(string command, string argumentsJson)
        {
            ExecuteCodeArgs args = ProtocolJson.Deserialize<ExecuteCodeArgs>(argumentsJson) ?? new ExecuteCodeArgs();
            if (string.IsNullOrWhiteSpace(args.code))
            {
                throw new CommandFailureException("INVALID_ARGS", "실행할 코드가 비어 있습니다.");
            }

            try
            {
                string wrappedCode = WrapperTemplate.Replace("/*__PUC_USER_CODE__*/", args.code);
                Assembly assembly = CompileCode(wrappedCode);
                Type? wrapperType = assembly.GetType("PucExecuteWrapper");
                if (wrapperType == null)
                {
                    throw new CommandFailureException("EXECUTE_FAILED", "래퍼 타입을 찾지 못했습니다.");
                }

                MethodInfo? executeMethod = wrapperType.GetMethod("Execute", BindingFlags.Public | BindingFlags.Static);
                if (executeMethod == null)
                {
                    throw new CommandFailureException("EXECUTE_FAILED", "Execute 메서드를 찾지 못했습니다.");
                }

                var logEntries = new List<string>();
                void OnLogMessageReceived(string condition, string stackTrace, LogType type)
                {
                    if (!string.IsNullOrWhiteSpace(condition))
                    {
                        logEntries.Add(condition);
                    }
                }

                Application.logMessageReceived += OnLogMessageReceived;
                try
                {
                    string consoleOutput = (string)(executeMethod.Invoke(null, null) ?? string.Empty);
                    string output = MergeOutput(consoleOutput, logEntries);
                    return ProtocolJson.Serialize(new ExecuteCodePayload
                    {
                        output = output,
                        success = true,
                    });
                }
                finally
                {
                    Application.logMessageReceived -= OnLogMessageReceived;
                }
            }
            catch (CommandFailureException)
            {
                throw;
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                return ProtocolJson.Serialize(new ExecuteCodePayload
                {
                    output = string.Empty,
                    success = false,
                    error = ex.InnerException.Message,
                });
            }
            catch (Exception ex)
            {
                return ProtocolJson.Serialize(new ExecuteCodePayload
                {
                    output = string.Empty,
                    success = false,
                    error = ex.Message,
                });
            }
        }

        private static Assembly CompileCode(string code)
        {
            using var provider = new CSharpCodeProvider();
            var parameters = new CompilerParameters
            {
                GenerateInMemory = true,
                GenerateExecutable = false,
            };

            var referencedAssemblies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(assembly.Location)
                        && referencedAssemblies.Add(assembly.Location))
                    {
                        parameters.ReferencedAssemblies.Add(assembly.Location);
                    }
                }
                catch
                {
                }
            }

            CompilerResults result = provider.CompileAssemblyFromSource(parameters, code);
            if (result.Errors.HasErrors)
            {
                var errors = new StringBuilder();
                foreach (CompilerError error in result.Errors)
                {
                    if (!error.IsWarning)
                    {
                        errors.AppendLine($"Line {error.Line}: {error.ErrorText}");
                    }
                }

                throw new CommandFailureException(
                    "COMPILE_FAILED",
                    "코드 컴파일에 실패했습니다:\n" + errors.ToString().TrimEnd());
            }

            return result.CompiledAssembly;
        }

        private static string MergeOutput(string consoleOutput, IEnumerable<string> logEntries)
        {
            string[] segments = new[] { consoleOutput.Trim(), string.Join(Environment.NewLine, logEntries).Trim() }
                .Where(segment => !string.IsNullOrWhiteSpace(segment))
                .ToArray();

            return segments.Length == 0 ? string.Empty : string.Join(Environment.NewLine, segments);
        }
    }
}
