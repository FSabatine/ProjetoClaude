import { useEffect, useRef, useState } from 'react';
import {
  ActionIcon, Alert, Anchor, Badge, Button, Chip, Drawer, Group, Loader, Paper, ScrollArea, Stack, Text, Textarea, Tooltip,
} from '@mantine/core';
import { IconSend, IconSparkles, IconTrash } from '@tabler/icons-react';
import { Link, useLocation } from 'react-router-dom';
import { useAuth } from '../../auth/AuthContext';
import { PERMISSIONS } from '../../auth/permissions';
import { toApiError } from '../../api/errors';
import {
  assistantApi, contextFromPath, contextualQuestions, onAssistantOpen, openAssistant, type AssistantAnswer,
} from './assistant';

interface Turn {
  question: string;
  answer?: AssistantAnswer;
  error?: string;
}

const MAX_LENGTH = 500;

/** Header button + right-side panel. Mounted once in AppLayout. */
export function AssistantButton() {
  const { can } = useAuth();
  if (!can(PERMISSIONS.assistant.use)) return null;
  return (
    <>
      <Tooltip label="Assistente da frota">
        <ActionIcon variant="subtle" color="gray" size="lg" onClick={() => openAssistant()} aria-label="Abrir o assistente da frota">
          <IconSparkles size={18} />
        </ActionIcon>
      </Tooltip>
      <AssistantPanel />
    </>
  );
}

function AssistantPanel() {
  const location = useLocation();
  const [opened, setOpened] = useState(false);
  const [text, setText] = useState('');
  const [turns, setTurns] = useState<Turn[]>([]);
  // The vehicle of the last answer: lets "e por que aumentou?" follow up without repeating the plate.
  const [focusVehicleId, setFocusVehicleId] = useState<string | null>(null);
  const status = assistantApi.useStatus(opened);
  const ask = assistantApi.useAsk();
  const bottom = useRef<HTMLDivElement>(null);
  const context = contextFromPath(location.pathname);

  const send = (question: string) => {
    const q = question.trim();
    if (!q || ask.isPending) return;
    const vehicleId = context.vehicleId ?? focusVehicleId;
    setTurns((t) => [...t, { question: q }]);
    setText('');
    ask.mutate({ question: q, vehicleId, page: context.vehicleId ? 'vehicle' : vehicleId ? 'vehicle' : context.page }, {
      onSuccess: (answer) => {
        if (answer.focusVehicleId) setFocusVehicleId(answer.focusVehicleId);
        setTurns((t) => t.map((turn, i) => (i === t.length - 1 ? { ...turn, answer } : turn)));
      },
      onError: (e) => setTurns((t) => t.map((turn, i) => (i === t.length - 1 ? { ...turn, error: toApiError(e).message } : turn))),
    });
  };

  // eslint-disable-next-line react-hooks/exhaustive-deps -- send uses the latest context at the moment of the event
  useEffect(() => onAssistantOpen((question) => { setOpened(true); if (question) setTimeout(() => send(question), 0); }), [location.pathname, focusVehicleId]);
  useEffect(() => bottom.current?.scrollIntoView({ behavior: 'smooth' }), [turns]);

  const suggestions = [...contextualQuestions(context), ...(status.data?.suggestions ?? [])].filter((q, i, all) => all.indexOf(q) === i).slice(0, 6);

  return (
    <Drawer opened={opened} onClose={() => setOpened(false)} position="right" size="lg" padding={0}
      title={<Group gap="xs"><IconSparkles size={18} /><Text fw={650}>Assistente da frota</Text></Group>}
      styles={{ body: { display: 'flex', flexDirection: 'column', height: 'calc(100% - 60px)' } }}>
      <ScrollArea style={{ flex: 1 }} px="md">
        <Stack gap="md" py="sm">
          <Alert variant="light" color="blue" p="sm">
            <Text size="sm">
              Responde com os dados da frota que <b>você</b> pode ver. Os números são calculados pelo sistema
              {status.data?.aiEnabled ? ' e a IA (Claude) só redige a explicação' : ''}. São sugestões — confira nas telas indicadas antes de decidir.
            </Text>
          </Alert>
          {turns.length === 0 && (
            <div>
              <Text size="sm" fw={600} mb={6}>Experimente perguntar</Text>
              <Chip.Group>
                <Group gap={6}>
                  {suggestions.map((s) => <Chip key={s} checked={false} variant="light" onClick={() => send(s)}>{s}</Chip>)}
                </Group>
              </Chip.Group>
            </div>
          )}
          {turns.map((turn, i) => (
            <Stack key={i} gap={6}>
              <Paper p="sm" bg="var(--mantine-color-default-hover)" style={{ alignSelf: 'flex-end', maxWidth: '85%' }}>
                <Text size="sm">{turn.question}</Text>
              </Paper>
              {!turn.answer && !turn.error && <Group gap="xs"><Loader size="xs" /><Text size="sm" c="dimmed">Consultando os dados…</Text></Group>}
              {turn.error && <Alert color="red" variant="light">{turn.error}</Alert>}
              {turn.answer && <AnswerCard answer={turn.answer} onSuggestion={send} onNavigate={() => setOpened(false)} />}
            </Stack>
          ))}
          <div ref={bottom} />
        </Stack>
      </ScrollArea>
      <Stack gap={6} p="md" style={{ borderTop: '1px solid var(--mantine-color-default-border)' }}>
        {(context.vehicleId || focusVehicleId) && (
          <Text size="xs" c="dimmed">{context.vehicleId ? 'Contexto: o veículo aberto nesta tela.' : 'Contexto: o último veículo citado na conversa.'}</Text>
        )}
        <Group gap="xs" align="flex-end" wrap="nowrap">
          <Textarea style={{ flex: 1 }} autosize minRows={1} maxRows={4} maxLength={MAX_LENGTH} placeholder="Pergunte sobre custos, consumo, manutenção, pneus, orçamento…"
            value={text} onChange={(e) => setText(e.currentTarget.value)} aria-label="Pergunta para o assistente"
            onKeyDown={(e) => { if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); send(text); } }} />
          <ActionIcon size={36} onClick={() => send(text)} loading={ask.isPending} disabled={!text.trim()} aria-label="Enviar pergunta"><IconSend size={18} /></ActionIcon>
        </Group>
        {turns.length > 0 && (
          <Button size="compact-xs" variant="subtle" color="gray" leftSection={<IconTrash size={12} />} w="fit-content"
            onClick={() => { setTurns([]); setFocusVehicleId(null); }}>Limpar conversa</Button>
        )}
      </Stack>
    </Drawer>
  );
}

function AnswerCard({ answer, onSuggestion, onNavigate }: { answer: AssistantAnswer; onSuggestion: (q: string) => void; onNavigate: () => void }) {
  return (
    <Paper withBorder p="sm" style={{ maxWidth: '95%' }}>
      <Stack gap={8}>
        <Badge variant="light" color={answer.mode === 'AiExplained' ? 'grape' : 'gray'} w="fit-content">
          {answer.mode === 'AiExplained' ? 'Explicado por IA a partir dos dados do sistema' : 'Calculado pelo sistema'}
        </Badge>
        <Text size="sm" fw={600}>{answer.answer}</Text>
        {answer.reason && <Text size="sm"><b>Motivo:</b> {answer.reason}</Text>}
        {answer.evidence.length > 0 && (
          <div>
            <Text size="sm" fw={600}>Evidências</Text>
            <Stack gap={2} component="ul" pl="md" m={0}>
              {answer.evidence.map((e) => <Text key={e} size="sm" component="li">{e}</Text>)}
            </Stack>
          </div>
        )}
        {answer.suggestedAction && <Text size="sm"><b>Sugestão:</b> {answer.suggestedAction}</Text>}
        {answer.notice && <Alert color="orange" variant="light" p="xs"><Text size="xs">{answer.notice}</Text></Alert>}
        {answer.sources.length > 0 && (
          <Group gap="xs">
            <Text size="xs" c="dimmed">Confira em:</Text>
            {answer.sources.map((s) => <Anchor key={s.link} component={Link} to={s.link} size="xs" onClick={onNavigate}>{s.label}</Anchor>)}
          </Group>
        )}
        {answer.insufficientData && answer.toolsUsed.length === 0 && (
          <Group gap={6}>
            {answer.suggestions.slice(0, 4).map((s) => <Chip key={s} size="xs" checked={false} variant="light" onClick={() => onSuggestion(s)}>{s}</Chip>)}
          </Group>
        )}
      </Stack>
    </Paper>
  );
}
