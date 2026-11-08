using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using YARG.Core.Logging;

namespace YARG.Core.Audio
{
    public enum AudioFxMode
    {
        Off,
        MultitrackOnly,
        On
    }

    public static class GlobalAudioHandler
    {
        public const int WHAMMY_FFT_DEFAULT = 2048;
        public const int WHAMMY_OVERSAMPLE_DEFAULT = 8;
        public static readonly int MAX_THREADS = Environment.ProcessorCount switch
        {
            >= 16 => 16,
            >= 6 => Environment.ProcessorCount / 2,
            _ => 2
        };

        internal static readonly Dictionary<SongStem, StemSettings> StemSettings;

        static GlobalAudioHandler()
        {
            StemSettings = new()
            {
                { SongStem.Song,     new StemSettings() },
                { SongStem.Guitar,   new StemSettings() },
                { SongStem.Bass,     new StemSettings() },
                { SongStem.Rhythm,   new StemSettings() },
                { SongStem.Keys,     new StemSettings() },
                { SongStem.Vocals,   new StemSettings() },
                { SongStem.Drums,    new StemSettings() },
                { SongStem.Crowd,    new StemSettings() },
                { SongStem.Sfx,      new StemSettings() },
                { SongStem.DrumSfx,  new StemSettings() },
                { SongStem.VoxSample, new StemSettings() },
                { SongStem.Metronome, new StemSettings() },
                { SongStem.Preview, new StemSettings() },
            };
        }

        public static bool LogMixerStatus { get; internal set; }

        public static bool UseWhammyFx;
        public static bool IsChipmunkSpeedup;

        /// <summary>
        /// The number of semitones to bend the pitch by. Must be at least 1;
        /// </summary>
        public static float WhammyPitchShiftAmount = 1f;

        /// <summary>
        /// The oversampling factor of the whammy SFX. Must be at least 4.
        /// </summary>
        /// <remarks>
        /// Changes to this value will not be applied until the next song plays.
        /// </remarks>
        public static int WhammyOversampleFactor = WHAMMY_OVERSAMPLE_DEFAULT;

        public static double GetTrueVolume(SongStem stem)
        {
            return StemSettings[stem].TrueVolume;
        }

        public static double GetVolumeSetting(SongStem stem)
        {
            return StemSettings[stem].VolumeSetting;
        }

        public static double GetVolumeMultiplier(SongStem stem)
        {
            return StemSettings[stem].VolumeMultiplier;
        }

        public static void SetVolumeSetting(SongStem stem, double volume)
        {
            StemSettings[stem].VolumeSetting = volume;
        }

        public static void SetVolumeMultiplier(SongStem stem, double multiplier)
        {
            StemSettings[stem].VolumeMultiplier = multiplier;
        }

        public static bool GetReverbSetting(SongStem stem)
        {
            return StemSettings[stem].Reverb;
        }

        public static void SetReverbSetting(SongStem stem, bool reverb)
        {
            StemSettings[stem].Reverb = reverb;
        }

        public static float GetWhammyPitchSetting(SongStem stem)
        {
            return StemSettings[stem].WhammyPitch;
        }

        public static void SetWhammyPitchSetting(SongStem stem, float percent)
        {
            StemSettings[stem].WhammyPitch = percent;
        }

        private static object _instanceLock = new();
        private static AudioManager? _instance;

        private static readonly string[] FALLBACK_FORMATS =
        {
            ".ogg", ".mogg", ".wav", ".mp3", ".aiff", ".opus",
        };

        public static void Initialize<TAudioManager>()
            where TAudioManager : AudioManager, new()
        {
            // Two locks to allow other things to happen
            lock (_instanceLock)
            {
                if (_instance == null || _instance is not TAudioManager)
                {
                    _instance?.Dispose();
                    _instance = new TAudioManager();
                }
            }
        }

        public static void Close()
        {
            lock (_instanceLock)
            {
                _instance?.Dispose();
                _instance = null;
            }
        }

        public static ReadOnlySpan<string> SupportedFormats
        {
            get
            {
                lock (_instanceLock)
                {
                    if (_instance == null)
                    {
                        return FALLBACK_FORMATS;
                    }
                    return _instance.SupportedFormats;
                }
            }
        }

        public static int PlaybackLatency
        {
            get
            {
                lock (_instanceLock)
                {
                    if (_instance == null)
                    {
                        return 0;
                    }
                    return _instance.PlaybackLatency;
                }
            }
        }

        public static int MinimumBufferLength
        {
            get
            {
                lock (_instanceLock)
                {
                    if (_instance == null)
                    {
                        return 10;
                    }
                    return _instance.MinimumBufferLength;
                }
            }
        }

        public static int MaximumBufferLength
        {
            get
            {
                lock (_instanceLock)
                {
                    if (_instance == null)
                    {
                        return 5000;
                    }
                    return _instance.MaximumBufferLength;
                }
            }
        }

        public static float GlobalSpeed
        {
            get
            {
                lock (_instanceLock)
                {
                    if (_instance == null)
                    {
                        return 1f;
                    }
                    return _instance.GlobalSpeed;
                }
            }
            set
            {
                lock (_instanceLock)
                {
                    if (_instance == null)
                    {
                        return;
                    }
                    _instance.GlobalSpeed = value;
                }
            }
        }

        public static void PlaySoundEffect(SfxSample sample, double duration = 0)
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    return;
                }
                _instance.SfxSamples[(int) sample]?.Play(duration);
            }
        }

        public static void StopSoundEffect(SfxSample sample, double duration = 0)
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    return;
                }
                _instance.SfxSamples[(int) sample]?.Stop(duration);
            }
        }

        public static void PauseSoundEffect(SfxSample sample)
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    return;
                }
                _instance.SfxSamples[(int) sample]?.Pause();
            }
        }

        public static void ResumeSoundEffect(SfxSample sample)
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    return;
                }
                _instance.SfxSamples[(int) sample]?.Resume();
            }
        }

        public static void PauseAllSfx()
        {
            foreach (var sample in AudioHelpers.SfxSamples)
            {
                if (sample.IsPlaying)
                {
                    PauseSoundEffect(sample.Kind);
                }
            }
        }

        public static void ResumeAllSfx()
        {
            foreach (var sample in AudioHelpers.SfxSamples)
            {
                if (sample.IsPlaying)
                {
                    ResumeSoundEffect(sample.Kind);
                }
            }
        }

        public static void PlayDrumSoundEffect(DrumSfxSample sample, double volume)
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    return;
                }
                _instance.DrumSfxSamples[(int) sample]?.Play(volume);
            }
        }

        public static void PlayVoxSample(VoxSample sample)
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    return;
                }

                _instance.VoxSamples[(int) sample]?.Play();
            }
        }

        public static void PlayMetronomeSoundEffect(MetronomeSample sample, MetronomePitch pitch)
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    return;
                }

                if (pitch == MetronomePitch.Hi)
                {
                    _instance.MetronomeSamples[(int) sample]?.PlayHi();

                    return;
                }

                _instance.MetronomeSamples[(int) sample]?.PlayLo();
            }
        }

        public static StemMixer? LoadCustomFile(string name, Stream stream, float speed, double volume, bool normalize = false, SongStem stem = SongStem.Song)
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    return null;
                }
                return _instance.LoadCustomFile(name, stream, speed, volume, normalize, stem);
            }
        }

        public static StemMixer? LoadCustomFile(string file, float speed, double volume, bool normalize = false, SongStem stem = SongStem.Song)
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    return null;
                }
                return _instance.LoadCustomFile(file, speed, volume, normalize, stem);
            }
        }

        public static StemMixer? CreateMixer(string name, float speed, double mixerVolume, bool clampStemVolume, bool normalize)
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    return null;
                }
                return _instance.CreateMixer(name, speed, mixerVolume, clampStemVolume: clampStemVolume, normalize: normalize);
            }
        }

        public static MicDevice? GetInputDevice(string name)
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    return null;
                }
                return _instance.GetInputDevice(name);
            }
        }

        public static List<(int id, string name)> GetAllInputDevices()
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    return new List<(int id, string name)>();
                }
                return _instance.GetAllInputDevices();
            }
        }

        public static MicDevice? CreateInputDevice(int deviceId, string name)
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    return null;
                }
                return _instance.CreateInputDevice(deviceId, name);
            }
        }

        public static void SetMasterVolume(double volume)
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    return;
                }
                _instance.SetMasterVolume(volume);
            }
        }


        public static void SetBufferLength(int length)
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    return;
                }
                _instance.SetBufferLength(length);
            }
        }

        public static List<(int id, string name)> GetAllOutputDevices()
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    return new List<(int id, string name)>();
                }
                return _instance.GetAllOutputDevices();
            }
        }

        public static int GetOutputChannelCount()
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    return 2;
                }
                return _instance.GetOutputChannelCount();
            }
        }

        public static OutputDevice? GetOutputDevice(string name)
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    return null;
                }
                return _instance.GetOutputDevice(name);
            }
        }

        public static void SetOutputChannel(SongStem stem, int channelId)
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    return;
                }

                OutputChannel? channel = _instance.CreateOutputChannel(channelId);
                if (channel == null)
                {
                    return;
                }

                switch (stem)
                {
                    case SongStem.DrumSfx:
                        foreach (DrumSampleChannel sample in _instance.DrumSfxSamples)
                        {
                            sample?.SetOutputChannel(channel);
                        }
                        break;

                    case SongStem.Metronome:
                        foreach (MetronomeSampleChannel sample in _instance.MetronomeSamples)
                        {
                            sample?.SetOutputChannel(channel);
                        }
                        break;

                    case SongStem.Sfx:
                        foreach (SampleChannel sample in _instance.SfxSamples)
                        {
                            sample?.SetOutputChannel(channel);
                        }
                        break;

                    case SongStem.VoxSample:
                        foreach (VoxSampleChannel sample in _instance.VoxSamples)
                        {
                            sample?.SetOutputChannel(channel);
                        }
                        break;

                    default:
                        _instance?.SetOutputChannel(channel);
                        break;
                }
            }
        }

        public static void StopAllSfxChannels()
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    return;
                }
                foreach (SampleChannel sample in _instance.SfxSamples)
                {
                    sample?.Stop();
                }
            }
        }

        public static void SetOutputDevice(string name)
        {
            lock (_instanceLock)
            {
                if (_instance == null)
                {
                    return;
                }
                _instance.SetOutputDevice(name);
            }
        }
    }
}
