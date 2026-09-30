import { useEffect, useState } from 'react';
import { ActionIcon, Anchor, Button, FileButton, Group, Image, Loader, Modal, Paper, Stack, Text, Tooltip } from '@mantine/core';
import { IconCamera, IconFileTypePdf, IconPaperclip, IconTrash } from '@tabler/icons-react';
import { useQuery } from '@tanstack/react-query';
import { filesApi, type FileInfo } from '../features/operations/api';
import { formatFileSize, prepareUpload } from '../lib/images';
import { notifyError } from './notify';

const ACCEPT = 'application/pdf,image/jpeg,image/png';

/** Blob URL of a protected file (the API needs the Bearer token, which an <img src> cannot send). */
function useFileUrl(id: string) {
  const query = useQuery({ queryKey: ['file', id], queryFn: () => filesApi.download(id), staleTime: Infinity });
  const [url, setUrl] = useState<string | null>(null);
  useEffect(() => {
    if (!query.data) return;
    const objectUrl = URL.createObjectURL(query.data);
    setUrl(objectUrl);
    return () => URL.revokeObjectURL(objectUrl);
  }, [query.data]);
  return { url, isLoading: query.isLoading, error: query.error };
}

function Thumbnail({ file, onOpen }: { file: FileInfo; onOpen: () => void }) {
  const { url } = useFileUrl(file.id);
  return (
    <Tooltip label={file.fileName}>
      <Paper
        component="button"
        type="button"
        onClick={onOpen}
        w={72}
        h={72}
        p={0}
        style={{ overflow: 'hidden', cursor: 'pointer' }}
        aria-label={`Ver foto ${file.fileName}`}
      >
        {url ? <Image src={url} alt="" w={72} h={72} fit="cover" /> : <Loader size="xs" m="auto" mt={28} />}
      </Paper>
    </Tooltip>
  );
}

function PhotoViewer({ file, onClose }: { file: FileInfo | null; onClose: () => void }) {
  return (
    <Modal opened={!!file} onClose={onClose} title={file?.fileName} size="xl" centered>
      {file && <PhotoBody file={file} />}
    </Modal>
  );
}

function PhotoBody({ file }: { file: FileInfo }) {
  const { url, isLoading } = useFileUrl(file.id);
  if (isLoading || !url) return <Loader />;
  return <Image src={url} alt={file.fileName} radius="md" />;
}

async function openPdf(file: FileInfo) {
  try {
    const blob = await filesApi.download(file.id);
    const url = URL.createObjectURL(blob);
    window.open(url, '_blank', 'noopener');
    setTimeout(() => URL.revokeObjectURL(url), 60_000);
  } catch (e) {
    notifyError(e, 'Não foi possível abrir o arquivo');
  }
}

/** Photos as thumbnails (tap to enlarge), PDFs as links. */
export function AttachmentList({ files, onRemove }: { files: FileInfo[]; onRemove?: (file: FileInfo) => void }) {
  const [viewing, setViewing] = useState<FileInfo | null>(null);
  if (files.length === 0) return null;
  const images = files.filter((f) => f.contentType.startsWith('image/'));
  const others = files.filter((f) => !f.contentType.startsWith('image/'));
  return (
    <Stack gap="xs">
      {images.length > 0 && (
        <Group gap="xs">
          {images.map((f) => (
            <div key={f.id} style={{ position: 'relative' }}>
              <Thumbnail file={f} onOpen={() => setViewing(f)} />
              {onRemove && (
                <ActionIcon size="sm" color="red" variant="filled" radius="xl" style={{ position: 'absolute', top: -6, right: -6 }}
                  aria-label={`Remover ${f.fileName}`} onClick={() => onRemove(f)}>
                  <IconTrash size={12} />
                </ActionIcon>
              )}
            </div>
          ))}
        </Group>
      )}
      {others.map((f) => (
        <Group key={f.id} gap="xs" wrap="nowrap">
          <IconFileTypePdf size={18} />
          <Anchor component="button" type="button" size="sm" onClick={() => void openPdf(f)} truncate>
            {f.fileName}
          </Anchor>
          <Text size="xs" c="dimmed">{formatFileSize(f.sizeBytes)}</Text>
          {onRemove && (
            <ActionIcon size="sm" variant="subtle" color="red" aria-label={`Remover ${f.fileName}`} onClick={() => onRemove(f)}>
              <IconTrash size={14} />
            </ActionIcon>
          )}
        </Group>
      ))}
      <PhotoViewer file={viewing} onClose={() => setViewing(null)} />
    </Stack>
  );
}

/**
 * Upload button. `camera` opens the phone camera directly (capture) — the fastest path for inspection photos.
 * Files are uploaded immediately and returned as FileInfo to be attached on save.
 */
export function UploadButton({ onUploaded, camera = false, label, disabled }: {
  onUploaded: (file: FileInfo) => void;
  camera?: boolean;
  label?: string;
  disabled?: boolean;
}) {
  const [uploading, setUploading] = useState(false);

  const handle = async (file: File | null) => {
    if (!file) return;
    setUploading(true);
    try {
      const { blob, name } = await prepareUpload(file);
      onUploaded(await filesApi.upload(blob, name));
    } catch (e) {
      notifyError(e, 'O arquivo não foi enviado');
    } finally {
      setUploading(false);
    }
  };

  return (
    <FileButton onChange={(f) => void handle(f)} accept={camera ? 'image/jpeg,image/png' : ACCEPT} capture={camera ? 'environment' : undefined}>
      {(props) => (
        <Button {...props} variant="default" loading={uploading} disabled={disabled}
          leftSection={camera ? <IconCamera size={18} /> : <IconPaperclip size={18} />}>
          {label ?? (camera ? 'Tirar foto' : 'Anexar arquivo')}
        </Button>
      )}
    </FileButton>
  );
}
