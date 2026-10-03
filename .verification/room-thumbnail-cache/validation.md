# Room selector thumbnail cache

Harness: `.verification/room-thumbnail-cache/CacheCheck` (needs `SCUMM_ROOM_CACHE_DIR` set to a private folder, so it cannot disturb the app's cache or another running instance).

Result on LOOM (72 scenes, rooms 82-85 excluded):

```
button open: cards=72 imagesImmediately=0
after loader: images=72/72 cachedPngs=72
reopen (cache): cards=72 imagesImmediately=72
reopen (incomplete cache): imagesImmediately=0 of 72
after loader: images=72/72 cachedPngs=72
  +3s cachedPngs=72 room03Back=True
  +6s cachedPngs=72 room03Back=True
  +9s cachedPngs=72 room03Back=True
  +12s cachedPngs=72 room03Back=True
```

- The Open button (`OpenGameFolder`) empties and rebuilds the cache; the async loader saves each PNG.
- Reopening the last game (`ReopenLastFolder`) shows all 72 thumbnails at once, with no loader.
- A missing room file makes the cache unusable: the loader runs and rebuilds the whole cache.
- The stamp (Core.dll build time, 00.LFL size and time) invalidates the cache after a new build or changed game data.
- Earlier runs that shared the app's cache folder gave 39-51 PNGs: another process was using the same folder. With a private folder the result is repeatable.
