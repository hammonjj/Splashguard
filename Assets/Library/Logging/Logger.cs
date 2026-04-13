using System;
using System.IO;
using System.Runtime.CompilerServices;
using BitBox.Library.Constants.Enums;
using UnityEngine;

namespace BitBox.Library.Logging
{
  public class Logger
  {
    private readonly string _objectName;
    private readonly int _instanceId;
    private readonly Func<LogLevel> _levelProvider;

    public Logger(string objectName, int instanceId, Func<LogLevel> levelProvider)
    {
      _objectName = objectName;
      _instanceId = instanceId;
      _levelProvider = levelProvider;
    }

    [HideInCallstack]
    private void Log(LogLevel level, string message, string filePath, int lineNumber)
    {
      if (_levelProvider() > level)
      {
        return;
      }

      var fileName = Path.GetFileName(filePath);
      var tag = $"[{_objectName}#{_instanceId}]";
      var text = $"{fileName}({lineNumber}): [{level}] {tag} {message}";

      switch (level)
      {
        case LogLevel.Debug: UnityEngine.Debug.Log(text); break;
        case LogLevel.Info: UnityEngine.Debug.Log(text); break;
        case LogLevel.Warning: UnityEngine.Debug.LogWarning(text); break;
        case LogLevel.Error: UnityEngine.Debug.LogError(text); break;
      }
    }

    [HideInCallstack]
    public void Debug(string msg,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
        => Log(LogLevel.Debug, msg, filePath, lineNumber);

    [HideInCallstack]
    public void Info(string msg,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
        => Log(LogLevel.Info, msg, filePath, lineNumber);

    [HideInCallstack]
    public void Warning(string msg,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
        => Log(LogLevel.Warning, msg, filePath, lineNumber);

    [HideInCallstack]
    public void Error(string msg,
        [CallerFilePath] string filePath = "",
        [CallerLineNumber] int lineNumber = 0)
        => Log(LogLevel.Error, msg, filePath, lineNumber);
  }
}
