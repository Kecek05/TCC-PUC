using System;
using UnityEditor;
using UnityEngine;

namespace Kecek.Editor
{
    /// <summary>
    /// Anything imported under Assets/Audio/Music gets the settings a phone wants for a long loop: streamed from
    /// disk rather than decompressed into memory (a two-minute stereo track is about 20 MB as PCM), Vorbis, and
    /// loaded in the background so a scene switch never waits on it.
    /// </summary>
    /// <remarks>Applied on the first import only, so a setting changed by hand in the Inspector is kept.</remarks>
    public class MusicImportSettings : AssetPostprocessor
    {
        private const string MusicFolder = "Assets/Audio/Music/";

        private void OnPreprocessAudio()
        {
            if (!assetPath.StartsWith(MusicFolder, StringComparison.OrdinalIgnoreCase)) return;
            if (!assetImporter.importSettingsMissing) return;

            AudioImporter importer = (AudioImporter)assetImporter;
            importer.forceToMono = false;
            importer.loadInBackground = true;

            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.Streaming;
            settings.compressionFormat = AudioCompressionFormat.Vorbis;
            settings.quality = 0.7f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            importer.defaultSampleSettings = settings;
        }
    }
}
