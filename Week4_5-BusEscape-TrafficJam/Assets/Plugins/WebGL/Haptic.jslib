mergeInto(LibraryManager.library, {
  Haptic_Vibrate: function (patternPtr) {
    if (typeof navigator === 'undefined' || !navigator.vibrate) return;
    var pattern = UTF8ToString(patternPtr).split(',').map(Number);
    navigator.vibrate(pattern);
  }
});