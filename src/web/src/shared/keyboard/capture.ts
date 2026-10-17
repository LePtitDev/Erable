/**
 * While the user records a new combination (preferences), the shortcuts of the application are suspended: pressing
 * Ctrl+K to give it to another action must not open the search (F15.R4, E2E-15-04).
 */
let recording = false;

export function setRecordingShortcut(value: boolean) {
  recording = value;
}

export function isRecordingShortcut(): boolean {
  return recording;
}
