import { expect, test } from "vite-plus/test";
import { detectObsRecordingFolder } from "../src/detect.ts";

// Trimmed from real OBS files on a Windows install. Note the leading UTF-8 BOM
// and CRLF endings, which OBS writes and the parser must tolerate.
const USER_INI =
  "﻿[General]\r\nFirstRun=true\r\n\r\n[Basic]\r\nProfile=Default\r\nProfileDir=Default\r\n";

const ADVANCED_BASIC =
  "﻿[General]\r\nName=Default\r\n\r\n[Output]\r\nMode=Advanced\r\n\r\n" +
  "[SimpleOutput]\r\nFilePath=E:/Simple Path\r\n\r\n" +
  "[AdvOut]\r\nRecType=Standard\r\nRecFilePath=E:/OBS Recordings\r\nRecFormat2=mp4\r\n";

const SIMPLE_BASIC = "﻿[Output]\r\nMode=Simple\r\n\r\n[SimpleOutput]\r\nFilePath=D:/Clips\r\n";

test("Advanced mode uses [AdvOut] RecFilePath", () => {
  expect(
    detectObsRecordingFolder({ userIni: USER_INI, profiles: { Default: ADVANCED_BASIC } }),
  ).toBe("E:/OBS Recordings");
});

test("Simple mode uses [SimpleOutput] FilePath", () => {
  expect(detectObsRecordingFolder({ userIni: USER_INI, profiles: { Default: SIMPLE_BASIC } })).toBe(
    "D:/Clips",
  );
});

test("missing [Output] Mode falls back to Simple output path", () => {
  const noMode = "﻿[SimpleOutput]\r\nFilePath=C:/Recordings\r\n";
  expect(detectObsRecordingFolder({ userIni: null, profiles: { Default: noMode } })).toBe(
    "C:/Recordings",
  );
});

test("active profile is selected by ProfileDir among several", () => {
  const result = detectObsRecordingFolder({
    userIni: "[Basic]\nProfileDir=Gaming\n",
    profiles: {
      Default: ADVANCED_BASIC,
      Gaming: "[Output]\nMode=Simple\n[SimpleOutput]\nFilePath=G:/Gaming\n",
    },
  });
  expect(result).toBe("G:/Gaming");
});

test("ProfileDir is matched case-insensitively", () => {
  const result = detectObsRecordingFolder({
    userIni: "[Basic]\nProfileDir=default\n",
    profiles: { Default: ADVANCED_BASIC },
  });
  expect(result).toBe("E:/OBS Recordings");
});

test("falls back to the first profile when user.ini is absent", () => {
  expect(detectObsRecordingFolder({ userIni: null, profiles: { Default: SIMPLE_BASIC } })).toBe(
    "D:/Clips",
  );
});

test("no profiles → undefined", () => {
  expect(detectObsRecordingFolder({ userIni: USER_INI, profiles: {} })).toBeUndefined();
});

test("empty recording path → undefined", () => {
  const empty = "[Output]\nMode=Advanced\n[AdvOut]\nRecFilePath=\n";
  expect(detectObsRecordingFolder({ userIni: null, profiles: { Default: empty } })).toBeUndefined();
});
