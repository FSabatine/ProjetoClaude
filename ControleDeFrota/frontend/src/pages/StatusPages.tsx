import { Button } from '@mantine/core';
import { IconAlertTriangle, IconLock, IconMapOff } from '@tabler/icons-react';
import { Link } from 'react-router-dom';
import { EmptyState } from '../components/States';

export function ForbiddenPage() {
  return (
    <EmptyState
      icon={<IconLock size={28} />}
      title="Acesso não permitido"
      description="Seu perfil de acesso não inclui esta área. Se você precisa dela para o seu trabalho, fale com o administrador do sistema."
      action={
        <Button component={Link} to="/" variant="default">
          Voltar ao painel
        </Button>
      }
    />
  );
}

export function NotFoundPage() {
  return (
    <EmptyState
      icon={<IconMapOff size={28} />}
      title="Página não encontrada"
      description="O endereço acessado não existe ou o registro foi excluído."
      action={
        <Button component={Link} to="/" variant="default">
          Voltar ao painel
        </Button>
      }
    />
  );
}

/** Route error boundary: an unexpected rendering failure, with a way out instead of a blank or technical screen. */
export function UnexpectedErrorPage() {
  return (
    <EmptyState
      icon={<IconAlertTriangle size={28} />}
      title="Algo deu errado nesta tela"
      description="Não foi possível exibir esta página. Recarregue para tentar de novo; se persistir, informe o suporte."
      action={<Button onClick={() => window.location.reload()}>Recarregar a página</Button>}
    />
  );
}
