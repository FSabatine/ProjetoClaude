import { useCallback, useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { listPessoas, softDeletePessoa } from '../api/pessoas'
import { extractErrorMessage } from '../api/errors'
import { useBreadcrumb } from '../layout/useBreadcrumb'
import type { PessoaDto } from '../types/pessoas'

const PAGE_SIZE = 20

function formatTipo(tipoPessoa: PessoaDto['tipoPessoa']): string {
  return tipoPessoa === 0 ? 'Física' : 'Jurídica'
}

export default function PessoasListPage() {
  useBreadcrumb([{ label: 'Pessoas' }])

  const [pessoas, setPessoas] = useState<PessoaDto[]>([])
  const [totalCount, setTotalCount] = useState(0)
  const [page, setPage] = useState(1)
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [confirmingId, setConfirmingId] = useState<string | null>(null)
  const [actionError, setActionError] = useState<string | null>(null)

  const load = useCallback((targetPage: number) => {
    setLoading(true)
    setError(null)
    listPessoas(targetPage, PAGE_SIZE)
      .then((result) => {
        setPessoas(result.items)
        setTotalCount(result.totalCount)
        setPage(result.page)
      })
      .catch((err) => {
        setError(extractErrorMessage(err, 'Não foi possível carregar as pessoas.'))
      })
      .finally(() => {
        setLoading(false)
      })
  }, [])

  useEffect(() => {
    load(1)
  }, [load])

  const totalPages = Math.max(1, Math.ceil(totalCount / PAGE_SIZE))

  async function handleInativar(id: string) {
    setActionError(null)
    try {
      await softDeletePessoa(id)
      setConfirmingId(null)
      load(page)
    } catch (err) {
      setActionError(extractErrorMessage(err, 'Não foi possível inativar a pessoa.'))
    }
  }

  return (
    <div className="card">
      <div className="list-toolbar">
        <h2 className="card-title" style={{ margin: 0 }}>
          Pessoas
        </h2>
        <Link to="/pessoas/novo" className="btn btn-primary">
          Nova pessoa
        </Link>
      </div>

      {error && <div className="error-banner">{error}</div>}
      {actionError && <div className="error-banner">{actionError}</div>}

      {loading ? (
        <p className="text-muted">Carregando...</p>
      ) : pessoas.length === 0 ? (
        <div className="empty-state">Nenhuma pessoa cadastrada.</div>
      ) : (
        <>
          <table className="data-table">
            <thead>
              <tr>
                <th>Nome</th>
                <th>Documento</th>
                <th>Tipo</th>
                <th>Perfis</th>
                <th>Ações</th>
              </tr>
            </thead>
            <tbody>
              {pessoas.map((pessoa) => (
                <tr key={pessoa.id}>
                  <td>{pessoa.nome}</td>
                  <td>{pessoa.cpfCnpj}</td>
                  <td>{formatTipo(pessoa.tipoPessoa)}</td>
                  <td>{pessoa.perfis.map((perfil) => perfil.nome).join(', ') || '-'}</td>
                  <td>
                    {confirmingId === pessoa.id ? (
                      <span className="inline-confirm">
                        <span className="text-muted">Confirmar inativação?</span>
                        <button type="button" className="btn btn-sm btn-danger-outline" onClick={() => handleInativar(pessoa.id)}>
                          Sim
                        </button>
                        <button type="button" className="btn btn-sm btn-outline" onClick={() => setConfirmingId(null)}>
                          Não
                        </button>
                      </span>
                    ) : (
                      <span className="table-actions">
                        <Link to={`/pessoas/${pessoa.id}/editar`} className="link-btn">
                          Editar
                        </Link>
                        <button type="button" className="link-btn" onClick={() => setConfirmingId(pessoa.id)}>
                          Inativar
                        </button>
                      </span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>

          <div className="pagination">
            <button type="button" className="btn btn-outline btn-sm" disabled={page <= 1} onClick={() => load(page - 1)}>
              Anterior
            </button>
            <span>
              Página {page} de {totalPages}
            </span>
            <button
              type="button"
              className="btn btn-outline btn-sm"
              disabled={page >= totalPages}
              onClick={() => load(page + 1)}
            >
              Próxima
            </button>
          </div>
        </>
      )}
    </div>
  )
}
