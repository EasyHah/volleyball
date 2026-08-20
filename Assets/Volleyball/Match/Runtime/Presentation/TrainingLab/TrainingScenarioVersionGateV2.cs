using System;
using System.Text;
using UnityEngine;

namespace Volleyball.Presentation.TrainingLab
{
    public sealed class TrainingScenarioSourceInspectionV2
    {
        internal TrainingScenarioSourceInspectionV2(
            int formatVersion,
            bool isSupported,
            string diagnostic)
        {
            FormatVersion = formatVersion;
            IsSupported = isSupported;
            Diagnostic = diagnostic;
        }

        public int FormatVersion { get; }
        public bool IsSupported { get; }
        public string Diagnostic { get; }
    }

    public static class TrainingScenarioVersionGateV2
    {
        [Serializable]
        private sealed class Header
        {
            public int formatVersion;
        }

        public static TrainingScenarioSourceInspectionV2 Inspect(byte[] sourceBytes)
        {
            if (sourceBytes == null) throw new ArgumentNullException(nameof(sourceBytes));
            Header header;
            try
            {
                header = JsonUtility.FromJson<Header>(
                    Encoding.UTF8.GetString(sourceBytes));
            }
            catch (ArgumentException)
            {
                return new TrainingScenarioSourceInspectionV2(
                    0, false, "TrainingLab 情景文件格式损坏。" );
            }

            if (header == null || header.formatVersion == 0)
                return new TrainingScenarioSourceInspectionV2(
                    0, false, "TrainingLab 情景文件缺少 formatVersion。" );
            if (header.formatVersion != TrainingScenarioTemplateV2.CurrentFormatVersion)
                return new TrainingScenarioSourceInspectionV2(
                    header.formatVersion,
                    false,
                    "不支持的 TrainingLab 情景版本；原文件保持不变。" );
            return new TrainingScenarioSourceInspectionV2(
                header.formatVersion, true, string.Empty);
        }
    }
}
