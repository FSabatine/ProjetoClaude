/** Longest side of photos taken on the phone. Enough to read a tire wear indicator; ~300 KB instead of ~5 MB. */
const MAX_DIMENSION = 1600;
const JPEG_QUALITY = 0.82;

/**
 * Shrinks camera photos before upload (mobile data, slow yards). PDFs and small images pass through untouched;
 * any failure falls back to the original file — the server validates the format anyway.
 */
export async function prepareUpload(file: File): Promise<{ blob: Blob; name: string }> {
  if (!file.type.startsWith('image/') || file.size < 400_000) return { blob: file, name: file.name };
  try {
    const bitmap = await createImageBitmap(file);
    const scale = Math.min(1, MAX_DIMENSION / Math.max(bitmap.width, bitmap.height));
    const canvas = document.createElement('canvas');
    canvas.width = Math.round(bitmap.width * scale);
    canvas.height = Math.round(bitmap.height * scale);
    canvas.getContext('2d')!.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
    const blob = await new Promise<Blob | null>((resolve) => canvas.toBlob(resolve, 'image/jpeg', JPEG_QUALITY));
    if (!blob || blob.size >= file.size) return { blob: file, name: file.name };
    return { blob, name: file.name.replace(/\.\w+$/, '') + '.jpg' };
  } catch {
    return { blob: file, name: file.name };
  }
}

export const formatFileSize = (bytes: number) =>
  bytes < 1024 * 1024 ? `${Math.max(1, Math.round(bytes / 1024))} KB` : `${(bytes / 1024 / 1024).toFixed(1).replace('.', ',')} MB`;
