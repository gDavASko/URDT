/**
 * Stream download of Qwen2.5 GGUF model weights directly to Drive E:
 * Enforces Zero Drive C: Pollution rule.
 */
import fs from 'node:fs';
import path from 'node:path';
import https from 'node:https';
import http from 'node:http';
import { URL } from 'node:url';

const DEFAULT_URL = 'https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct-GGUF/resolve/main/qwen2.5-1.5b-instruct-q4_k_m.gguf';
const DEFAULT_TARGET = path.resolve('E:/Projects/URDT/CoreAgent/models/qwen2.5-1.5b-instruct-q4_k_m.gguf');
function getArg(flag) {
  const idx = process.argv.indexOf(flag);
  return idx !== -1 && idx + 1 < process.argv.length ? process.argv[idx + 1] : null;
}

const minSizeArg = getArg('--min-size');
const EXPECTED_MIN_SIZE = minSizeArg ? parseInt(minSizeArg, 10) : 350_000_000;

const targetPath = path.resolve(getArg('--target') || DEFAULT_TARGET);
const downloadUrl = getArg('--url') || DEFAULT_URL;

// Iron Rule 1: Zero Drive C: Pollution
if (targetPath.toLowerCase().startsWith('c:')) {
  console.error(`[FATAL] Iron Rule Violation: Attempted to download model to Drive C: (${targetPath})`);
  process.exit(1);
}

const targetDir = path.dirname(targetPath);
if (!fs.existsSync(targetDir)) {
  fs.mkdirSync(targetDir, { recursive: true });
}

// Check if already completely downloaded
if (fs.existsSync(targetPath)) {
  const stat = fs.statSync(targetPath);
  if (stat.size >= EXPECTED_MIN_SIZE) {
    console.log(`[URDT] Model already exists and verified: ${targetPath} (${(stat.size / 1024 / 1024).toFixed(2)} MB)`);
    process.exit(0);
  } else {
    console.log(`[URDT] Incomplete model found (${stat.size} bytes). Resuming download...`);
  }
}

console.log(`[URDT] Starting direct streaming download to Drive E:`);
console.log(`  Source: ${downloadUrl}`);
console.log(`  Target: ${targetPath}`);

function downloadWithRedirect(urlStr, destPath, maxRedirects = 10) {
  if (maxRedirects <= 0) {
    console.error('[FATAL] Too many HTTP redirects');
    process.exit(1);
  }

  const parsedUrl = new URL(urlStr);
  const client = parsedUrl.protocol === 'https:' ? https : http;

  const req = client.get(urlStr, {
    headers: {
      'User-Agent': 'URDT-Autonomous-Agent/2.0'
    }
  }, (res) => {
    // Handle HTTP Redirects
    if (res.statusCode >= 300 && res.statusCode < 400 && res.headers.location) {
      const nextUrl = new URL(res.headers.location, urlStr).toString();
      console.log(`[URDT] Following redirect -> ${nextUrl.substring(0, 80)}...`);
      return downloadWithRedirect(nextUrl, destPath, maxRedirects - 1);
    }

    if (res.statusCode !== 200) {
      console.error(`[FATAL] HTTP Error: ${res.statusCode} ${res.statusMessage}`);
      process.exit(1);
    }

    const totalBytes = parseInt(res.headers['content-length'] || '0', 10);
    let downloadedBytes = 0;
    let lastPercent = -1;

    console.log(`[URDT] Remote Content-Length: ${(totalBytes / 1024 / 1024).toFixed(2)} MB`);

    const fileStream = fs.createWriteStream(destPath);

    res.on('data', (chunk) => {
      downloadedBytes += chunk.length;
      if (totalBytes > 0) {
        const percent = Math.floor((downloadedBytes / totalBytes) * 100);
        if (percent % 10 === 0 && percent !== lastPercent) {
          lastPercent = percent;
          console.log(`[URDT] Download progress: ${percent}% (${(downloadedBytes / 1024 / 1024).toFixed(1)} / ${(totalBytes / 1024 / 1024).toFixed(1)} MB)`);
        }
      }
    });

    res.pipe(fileStream);

    fileStream.on('finish', () => {
      fileStream.close(() => {
        const finalStat = fs.statSync(destPath);
        console.log(`[URDT] Download complete! Saved to ${destPath} (${(finalStat.size / 1024 / 1024).toFixed(2)} MB)`);
        if (finalStat.size < EXPECTED_MIN_SIZE) {
          console.error(`[FATAL] File size too small: ${finalStat.size} bytes. Expected > ${EXPECTED_MIN_SIZE}`);
          process.exit(1);
        }
        process.exit(0);
      });
    });

    fileStream.on('error', (err) => {
      console.error(`[FATAL] File stream error: ${err.message}`);
      process.exit(1);
    });
  });

  req.on('error', (err) => {
    console.error(`[FATAL] Network error: ${err.message}`);
    process.exit(1);
  });
}

downloadWithRedirect(downloadUrl, targetPath);
