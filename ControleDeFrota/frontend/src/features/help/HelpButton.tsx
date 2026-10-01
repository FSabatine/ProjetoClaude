import { ActionIcon, Tooltip } from '@mantine/core';
import { useDisclosure } from '@mantine/hooks';
import { IconHelpCircle } from '@tabler/icons-react';
import { HelpDrawer } from './HelpDrawer';

/** The "?" entry point (seção 2/33 do pedido) — ao lado do seletor de tema, no cabeçalho. */
export function HelpButton() {
  const [opened, { open, close }] = useDisclosure(false);

  return (
    <>
      <Tooltip label="Ajuda e manual do usuário">
        <ActionIcon variant="subtle" color="gray" size="lg" onClick={open} aria-label="Ajuda e manual do usuário">
          <IconHelpCircle size={18} />
        </ActionIcon>
      </Tooltip>
      <HelpDrawer opened={opened} onClose={close} />
    </>
  );
}
