namespace RF4AssistantPro.Services;

public sealed class CaptureWorkflowCoordinator
{
    private readonly object _gate = new();
    private bool _captureInProgress;
    private bool _baitArmed;
    private bool _waterBodyArmed;
    private bool _keepnetArmed;
    private bool _keepnetSeriesRunning;

    public CaptureWorkflowState State
    {
        get
        {
            lock (_gate)
            {
                return CreateState();
            }
        }
    }

    public void ArmBait()
    {
        lock (_gate)
        {
            _baitArmed = true;
        }
    }

    public void ArmWaterBody()
    {
        lock (_gate)
        {
            _waterBodyArmed = true;
        }
    }

    public void ArmKeepnet()
    {
        lock (_gate)
        {
            _keepnetArmed = true;
        }
    }

    public bool TryBeginCapture()
    {
        lock (_gate)
        {
            if (_captureInProgress)
            {
                return false;
            }

            _captureInProgress = true;
            return true;
        }
    }

    public bool TryBeginBaitCapture()
    {
        lock (_gate)
        {
            if (!_baitArmed || _captureInProgress)
            {
                return false;
            }

            _baitArmed = false;
            _captureInProgress = true;
            return true;
        }
    }

    public bool TryBeginWaterBodyCapture()
    {
        lock (_gate)
        {
            if (!_waterBodyArmed || _captureInProgress)
            {
                return false;
            }

            _waterBodyArmed = false;
            _captureInProgress = true;
            return true;
        }
    }

    public bool TryStartKeepnetSeries()
    {
        lock (_gate)
        {
            if (!_keepnetArmed ||
                _keepnetSeriesRunning ||
                _captureInProgress)
            {
                return false;
            }

            _keepnetArmed = false;
            _keepnetSeriesRunning = true;
            return true;
        }
    }

    public bool TryBeginKeepnetCapture()
    {
        lock (_gate)
        {
            if (!_keepnetSeriesRunning || _captureInProgress)
            {
                return false;
            }

            _captureInProgress = true;
            return true;
        }
    }

    public void EndCapture()
    {
        lock (_gate)
        {
            _captureInProgress = false;
        }
    }

    public void EndKeepnetSeries()
    {
        lock (_gate)
        {
            _keepnetSeriesRunning = false;
            _captureInProgress = false;
        }
    }

    private CaptureWorkflowState CreateState()
    {
        return new CaptureWorkflowState(
            _captureInProgress,
            _baitArmed,
            _waterBodyArmed,
            _keepnetArmed,
            _keepnetSeriesRunning);
    }
}

public sealed record CaptureWorkflowState(
    bool CaptureInProgress,
    bool BaitArmed,
    bool WaterBodyArmed,
    bool KeepnetArmed,
    bool KeepnetSeriesRunning);
