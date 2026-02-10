using Final_Test_Hybrid.Models.Errors;
using Final_Test_Hybrid.Services.Common.Logging;

namespace Final_Test_Hybrid.Services.Errors;

public sealed class ErrorService(
    DualLogger<ErrorService> logger) : IErrorService
{
    private const int MaxHistorySize = 1000;

    private readonly Lock _errorsLock = new();
    private readonly List<ActiveError> _activeErrors = [];
    private readonly List<ErrorHistoryItem> _history = [];

    public event Action? OnActiveErrorsChanged;
    public event Action? OnHistoryChanged;

    public bool HasResettableErrors
    {
        get
        {
            lock (_errorsLock)
            {
                return _activeErrors.Any(e => e.ActivatesResetButton);
            }
        }
    }

    public bool HasActiveErrors
    {
        get
        {
            lock (_errorsLock)
            {
                return _activeErrors.Count > 0;
            }
        }
    }

    private bool _isHistoryEnabled;
    public bool IsHistoryEnabled
    {
        get => _isHistoryEnabled;
        set
        {
            if (_isHistoryEnabled == value)
                return;

            _isHistoryEnabled = value;

            if (value)
            {
                AddActiveErrorsToHistory();
            }
            else
            {
                CloseAllOpenHistoryRecords();
            }
        }
    }

    public IReadOnlyList<ActiveError> GetActiveErrors()
    {
        lock (_errorsLock)
        {
            return _activeErrors.ToList();
        }
    }

    public IReadOnlyList<ErrorHistoryItem> GetHistory()
    {
        lock (_errorsLock)
        {
            return _history.ToList();
        }
    }

    public void Raise(ErrorDefinition error, string? details = null)
    {
        AddError(error, ErrorSource.Application, null, null, details);
    }

    public void RaiseInStep(ErrorDefinition error, string stepId, string stepName, string? details = null)
    {
        AddError(error, ErrorSource.Application, stepId, stepName, details);
    }

    public void RaisePlc(ErrorDefinition error, string? stepId = null, string? stepName = null)
    {
        AddError(error, ErrorSource.Plc, stepId, stepName, null);
    }

    public void Clear(string errorCode)
    {
        RemoveError(errorCode);
    }

    public void ClearPlc(string errorCode)
    {
        RemoveError(errorCode);
    }

    public void ClearActiveApplicationErrors()
    {
        var removed = RemoveErrorsMatching(e => e.Source == ErrorSource.Application);
        if (removed)
        {
            NotifyChanges();
        }
    }

    public void ClearAllActiveErrors()
    {
        var removed = RemoveAllErrors();
        if (removed)
        {
            NotifyChanges();
        }
    }

    private bool RemoveErrorsMatching(Func<ActiveError, bool> predicate)
    {
        lock (_errorsLock)
        {
            var toRemove = _activeErrors.Where(predicate).ToList();
            foreach (var error in toRemove)
            {
                _activeErrors.Remove(error);
                CloseHistoryRecord(error.Code);
            }
            return toRemove.Count > 0;
        }
    }

    private bool RemoveAllErrors()
    {
        lock (_errorsLock)
        {
            if (_activeErrors.Count == 0)
            {
                return false;
            }
            foreach (var error in _activeErrors)
            {
                CloseHistoryRecord(error.Code);
            }
            _activeErrors.Clear();
            return true;
        }
    }

    public void ClearHistory()
    {
        bool hadItems;
        lock (_errorsLock)
        {
            hadItems = _history.Count > 0;
            _history.Clear();
        }
        if (hadItems)
        {
            OnHistoryChanged?.Invoke();
        }
    }

    private void AddError(ErrorDefinition def, ErrorSource source,
        string? stepId, string? stepName, string? details)
    {
        ActiveError? existingError;

        lock (_errorsLock)
        {
            existingError = _activeErrors.FirstOrDefault(e => e.Code == def.Code);
            if (existingError is null)
            {
                var now = DateTime.Now;
                var description = BuildDescription(def, details);
                var addedError = CreateActiveError(def, source, stepId, stepName, description, now);
                _activeErrors.Add(addedError);
                AddToHistory(CreateHistoryItem(def, source, stepId, stepName, description, now));
            }
        }

        if (existingError is not null)
        {
            logger.LogWarning(
                "ErrorService duplicate raise: Code={Code}, ExistingActivatesResetButton={ExistingFlag}, IncomingActivatesResetButton={IncomingFlag}, Source={Source}",
                def.Code,
                existingError.ActivatesResetButton,
                def.ActivatesResetButton,
                source);
            return;
        }

        NotifyChanges();
    }

    private void RemoveError(string errorCode)
    {
        var removed = false;
        lock (_errorsLock)
        {
            var index = _activeErrors.FindIndex(e => e.Code == errorCode);
            if (index >= 0)
            {
                _activeErrors.RemoveAt(index);
                CloseHistoryRecord(errorCode);
                removed = true;
            }
        }
        if (removed)
        {
            NotifyChanges();
            return;
        }
    }

    private static string BuildDescription(ErrorDefinition def, string? details)
        => string.IsNullOrEmpty(details) ? def.Description : $"{def.Description}: {details}";

    private static ActiveError CreateActiveError(ErrorDefinition def, ErrorSource source,
        string? stepId, string? stepName, string description, DateTime time)
    {
        return new ActiveError
        {
            Time = time,
            Code = def.Code,
            Description = description,
            Severity = def.Severity,
            Source = source,
            StepId = stepId,
            StepName = stepName,
            ActivatesResetButton = def.ActivatesResetButton
        };
    }

    private static ErrorHistoryItem CreateHistoryItem(ErrorDefinition def, ErrorSource source,
        string? stepId, string? stepName, string description, DateTime time)
    {
        return new ErrorHistoryItem
        {
            StartTime = time,
            Code = def.Code,
            Description = description,
            Severity = def.Severity,
            Source = source,
            StepId = stepId,
            StepName = stepName
        };
    }

    private void AddToHistory(ErrorHistoryItem item)
    {
        if (!IsHistoryEnabled)
        {
            return;
        }

        AddToHistoryInternal(item);
    }

    private void AddToHistoryInternal(ErrorHistoryItem item)
    {
        if (_history.Count >= MaxHistorySize)
        {
            _history.RemoveAt(0);
        }
        _history.Add(item);
    }

    private void AddActiveErrorsToHistory()
    {
        bool hasErrors;
        lock (_errorsLock)
        {
            foreach (var error in _activeErrors)
            {
                var historyItem = new ErrorHistoryItem
                {
                    StartTime = error.Time,
                    Code = error.Code,
                    Description = error.Description,
                    Severity = error.Severity,
                    Source = error.Source,
                    StepId = error.StepId,
                    StepName = error.StepName
                };

                AddToHistoryInternal(historyItem);
            }

            hasErrors = _activeErrors.Count > 0;
        }

        if (hasErrors)
        {
            OnHistoryChanged?.Invoke();
        }
    }

    private void CloseHistoryRecord(string errorCode)
    {
        for (var i = _history.Count - 1; i >= 0; i--)
        {
            if (_history[i].Code == errorCode && _history[i].EndTime == null)
            {
                _history[i] = _history[i] with { EndTime = DateTime.Now };
                return;
            }
        }
    }

    /// <summary>
    /// Закрывает все открытые записи в истории (устанавливает EndTime).
    /// </summary>
    private void CloseAllOpenHistoryRecords()
    {
        var now = DateTime.Now;
        bool hasChanges;
        lock (_errorsLock)
        {
            hasChanges = false;
            for (var i = 0; i < _history.Count; i++)
            {
                if (_history[i].EndTime == null)
                {
                    _history[i] = _history[i] with { EndTime = now };
                    hasChanges = true;
                }
            }
        }
        if (hasChanges)
        {
            OnHistoryChanged?.Invoke();
        }
    }

    private void NotifyChanges()
    {
        OnActiveErrorsChanged?.Invoke();
        OnHistoryChanged?.Invoke();
    }
}
