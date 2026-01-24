// initial source: https://github.com/snakers4/silero-vad/tree/master/examples/csharp

using System.Diagnostics;
using VadService;

const int SAMPLE_RATE = 16000;
const string MODEL_PATH = "./resources/silero_vad.onnx";
const float THRESHOLD = 0.42f; // tweaked with this a a bit, this seems to be the best option
const double SILENCE_DURATION_SECONDS = 1.5; // to not constantly trigger on/off add a silence duration to allow for brief pauses in speech

try
{
    var vad = new VadStreamDetector(MODEL_PATH, THRESHOLD, SAMPLE_RATE);
    int frameSize = 512;
    int bytesPerFrame = frameSize * 2;

    bool isSpeaking = false;
    DateTime speechStartTime = DateTime.MinValue;
    DateTime? silenceStartTime = null;
    int totalSpeechSegments = 0;
    double totalSpeechDuration = 0.0;

    // On Windows use Naudio to detect microphone (not tested)
    var process = new Process
    {
        StartInfo = new ProcessStartInfo
        {
            FileName = "arecord",
            Arguments = $"-f S16_LE -r {SAMPLE_RATE} -c 1 -t raw",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        }
    };

    process.Start();
    Console.WriteLine("\n\nRecording started...");


    var stream = process.StandardOutput.BaseStream;
    byte[] buffer = new byte[bytesPerFrame];
    float[] audioFrame = new float[frameSize];

    var cancellationSource = new CancellationTokenSource();
    Console.CancelKeyPress += (sender, args) =>
    {
        args.Cancel = true;
        cancellationSource.Cancel();
    };

    var readTask = Task.Run(async () =>
    {
        try
        {
            while (!cancellationSource.Token.IsCancellationRequested)
            {
                // Read exactly one frame of audio data
                int bytesRead = 0;
                while (bytesRead < bytesPerFrame && !cancellationSource.Token.IsCancellationRequested)
                {
                    int read = await stream.ReadAsync(buffer.AsMemory(bytesRead, bytesPerFrame - bytesRead), cancellationSource.Token);
                    if (read == 0) return;
                    bytesRead += read;
                }

                //if bytesRead is not yet filled up but cancellation is requested it exits the loop above and then breaks here (i know its not the best code...)
                if (bytesRead < bytesPerFrame) break;

                //convert bytes (16-bit PCM) to float samples (-1.0 to 1.0)
                for (int i = 0; i < frameSize; i++)
                {
                    short sample = BitConverter.ToInt16(buffer, i * 2);
                    audioFrame[i] = sample / 32768f;
                }

                var result = vad.ProcessFrame(audioFrame);

                if (result.IsSpeech)
                {
                    if (!isSpeaking)
                    {
                        speechStartTime = DateTime.Now;
                        totalSpeechSegments++;
                        Console.ForegroundColor = ConsoleColor.Green; // Just for clear Logging
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ▶ Speech START (prob: {result.SpeechProbability:F3})");
                        Console.ResetColor();
                        isSpeaking = true;
                    }

                    //Reset silence timer
                    silenceStartTime = null;
                }
                else
                {
                    if (isSpeaking)
                    {
                        if (silenceStartTime == null)
                        {
                            silenceStartTime = DateTime.Now;
                        }
                        else
                        {
                            var silenceDuration = (DateTime.Now - silenceStartTime.Value).TotalSeconds;
                            if (silenceDuration >= SILENCE_DURATION_SECONDS)
                            {
                                var speechDuration = (silenceStartTime.Value - speechStartTime).TotalSeconds;
                                totalSpeechDuration += speechDuration;
                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ■ Speech END (prob: {result.SpeechProbability:F3}, duration: {speechDuration:F2}s)");
                                Console.ResetColor();
                                isSpeaking = false;
                                silenceStartTime = null;
                            }
                        }
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Normal cancellation
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading audio: {ex.Message}");
        }
    });

    readTask.Wait();

    // Clean up the process
    try
    {
        process.Kill();
        process.WaitForExit();
    }
    catch { }

    Console.WriteLine("\nstatistics");
    Console.WriteLine($"Amount of speech segments: {totalSpeechSegments}");
    Console.WriteLine($"Total speech duration: {totalSpeechDuration}s");
    if (totalSpeechSegments > 0)
    {
        Console.WriteLine($"Average segment duration: {(totalSpeechDuration / totalSpeechSegments)}s");
    }
    Console.WriteLine();
    Console.WriteLine("Recording stopped.");
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine($"Stack trace: {ex.StackTrace}");
    Console.ResetColor();
}