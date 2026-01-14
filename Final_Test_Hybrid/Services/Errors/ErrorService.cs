using Final_Test_Hybrid.Models.Errors;

namespace Final_Test_Hybrid.Services.Errors;

public sealed class ErrorService : IErrorService
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

    public bool IsHistoryEnabled { get; set; } = false;

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
        lock (_errorsLock)
        {
            if (IsErrorAlreadyActive(def.Code))
            {
                return;
            }
            var now = DateTime.Now;
            var description = BuildDescription(def, details);
            _activeErrors.Add(CreateActiveError(def, source, stepId, stepName, description, now));
            AddToHistory(CreateHistoryItem(def, source, stepId, stepName, description, now));
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
        }
    }

    private bool IsErrorAlreadyActive(string code)
        => _activeErrors.Any(e => e.Code == code);

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

        if (_history.Count >= MaxHistorySize)
        {
            _history.RemoveAt(0);
        }
        _history.Add(item);
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

    private void NotifyChanges()
    {
        OnActiveErrorsChanged?.Invoke();
        OnHistoryChanged?.Invoke();
    }
}
