// author: https://github.com/snakers4/silero-vad/tree/master/examples/csharp
// author: NVLY

using System.Diagnostics;
using VadService;

const int SAMPLE_RATE = 16000;
const string MODEL_PATH = "./resources/silero_vad.onnx";
const float THRESHOLD = 0.42f; // NVLY: tweaked with this a a bit, this seems to be the best option
const double SILENCE_DURATION_SECONDS = 2; // NVLY: to not constantly trigger on/off add a silence duration to allow for brief pauses in speech

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

    // NVLY: On Windows use Naudio to detect microphone
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

    //NVLY: start process to use mic as input
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

                //NVLY: if bytesRead is not yet filled up but cancellation is requested it exits the loop above and then breaks here (i know its not the best code...)
                if (bytesRead < bytesPerFrame) break;

                // Convert bytes (16-bit PCM) to float samples (-1.0 to 1.0)
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
                        Console.ForegroundColor = ConsoleColor.Green; // NVLY: Just for clear Logging
                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] ▶ Speech START (prob: {result.SpeechProbability:F3})");
                        Console.ResetColor();
                        isSpeaking = true;
                    }
                    
                    //NVLY: Reset silence timer
                    silenceStartTime = null;
                }
                else
                {
                    if (isSpeaking)
                    {
                        // NVLY: Used to be speaking, now silent
                        if (silenceStartTime == null)
                        {
                            // First frame of silence - start the timer
                            silenceStartTime = DateTime.Now;
                        }
                        else
                        {
                            // NVLY: Compare time of silence and the silence duration parameter
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
        // stop reading the microphone
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




//NVLY: The below code does the same but instead of from streaming it uses a .wav file as input
// using NAudio.Wave;
// using VadService;

// const int FRAME_SIZE = 512; 
// const int SAMPLE_RATE = 16000;
// const string MODEL_PATH = "./resources/silero_vad.onnx";
// const string EXAMPLE_WAV_FILE = "./resources/test.wav";
// const float THRESHOLD = 0.5f;


// using var reader = new AudioFileReader(EXAMPLE_WAV_FILE);

// var vad = new VadStreamDetector(
//     MODEL_PATH,
//     THRESHOLD,
//     SAMPLE_RATE

// );

// float[] buffer = new float[FRAME_SIZE];
// bool isSpeaking = false;
// double currentTime = 0.0;

// Console.WriteLine(DateTime.Now);
// while (true)
// {
//     int samplesRead = reader.Read(buffer, 0, FRAME_SIZE);
//     if (samplesRead == 0) break;

//     var result = vad.ProcessFrame(buffer);

//     if (result.IsSpeech && !isSpeaking)
//     {
//         Console.WriteLine($"Speech started at {currentTime:F2}s");
//         isSpeaking = true;
//     }
//     else if (!result.IsSpeech && isSpeaking)
//     {
//         Console.WriteLine($"Speech ended at {currentTime:F2}s");
//         isSpeaking = false;
//     }

//     currentTime += (double)samplesRead / SAMPLE_RATE;
//     Thread.Sleep((int)(1000.0 * samplesRead / SAMPLE_RATE));
// }
// Console.WriteLine(DateTime.Now);