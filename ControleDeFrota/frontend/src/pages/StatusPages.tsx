import { Button } from '@mantine/core';
import { IconLock, IconMapOff } from '@tabler/icons-react';
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
