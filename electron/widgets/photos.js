const fs = require('node:fs')
const path = require('node:path')

const IMAGE = /\.(jpe?g|png|gif|webp|avif|bmp)$/i
const MAX_PHOTOS = 200

// Image file names in the folder (not recursive), newest first.
function listPhotos(dir) {
  try {
    return fs.readdirSync(dir)
      .filter((n) => IMAGE.test(n))
      .map((n) => ({ n, t: fs.statSync(path.join(dir, n)).mtimeMs }))
      .sort((a, b) => b.t - a.t)
      .slice(0, MAX_PHOTOS)
      .map((x) => x.n)
  } catch {
    return []
  }
}

// Maps peninsula-photo://img/<name> to a file in the folder; refuses anything that escapes it.
function resolvePhoto(dir, url) {
  const name = decodeURIComponent(new URL(url).pathname.replace(/^\//, ''))
  const file = path.resolve(dir, name)
  if (path.dirname(file) !== path.resolve(dir) || !IMAGE.test(file)) return null
  return file
}

module.exports = { listPhotos, resolvePhoto, IMAGE }
