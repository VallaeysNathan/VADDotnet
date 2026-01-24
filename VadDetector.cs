namespace VadService;

public class VadStreamDetector
{
    private readonly VadOnnxModel _model;
    private readonly float _threshold;
    private readonly int _samplingRate;
    private readonly int _windowSizeSample;

    public VadStreamDetector(string onnxModelPath, float threshold, int samplingRate)
    {
        if (samplingRate != 8000 && samplingRate != 16000)
        {
            throw new ArgumentException("Sampling rate not supported, only available for [8000, 16000]");
        }

        this._model = new VadOnnxModel(onnxModelPath);
        this._samplingRate = samplingRate;
        this._threshold = threshold;
        this._windowSizeSample = samplingRate == 16000 ? 512 : 256;
    }

    public void Reset()
    {
        _model.ResetStates();
    }

    public VadResult ProcessFrame(float[] audioFrame)
    {
        if (audioFrame.Length != _windowSizeSample)
        {
            throw new ArgumentException($"Frame size must be {_windowSizeSample} samples");
        }

        float speechProb = _model.Call([audioFrame], _samplingRate)[0];
        
        return new VadResult
        {
            SpeechProbability = speechProb,
            IsSpeech = speechProb >= _threshold
        };
    }
}

public class VadResult
{
    public float SpeechProbability { get; set; }
    public bool IsSpeech { get; set; }
}