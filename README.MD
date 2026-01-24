# C# VAD Service experiment (with audio stream)

This is a small dotnet console application where a VAD model is used to detect when a person has started speaking with the input being an audio stream from the microphone.

Depending on the OS the code to get the input from the microphone could need to be tweaked. (This was developed on Ubuntu24.04)

Model and starting point of the code came from this github repo: https://github.com/snakers4/silero-vad/tree/master/examples/csharp
the linked github repo is under the MIT license as is this one.
