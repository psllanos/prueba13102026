import type { ResultadoOptimizacion } from '../../shared/types';
import { ETIQUETA_FACTOR, UNIDAD_FACTOR } from '../../shared/types';
import { formatearEta } from '../../shared/geo';

/**
 * Muestra POR QUÉ se eligió un proveedor: cada factor con su peso y su contribución.
 *
 * La API devuelve el desglose justamente para que la decisión sea auditable y no un número suelto.
 * Una UI que sólo mostrara "puntaje: 0,87" estaría tirando esa información a la basura.
 *
 * Se ordenan los factores por contribución descendente: lo que más pesó en la decisión aparece
 * primero, que es el orden en el que el usuario quiere leerlo.
 */
export function DesglosePuntaje({ resultado }: { resultado: ResultadoOptimizacion }) {
  const { selectedProvider: elegido, breakdown, totalScore, caseId, decidedAt } = resultado;

  const factores = [...breakdown].sort((a, b) => b.contribution - a.contribution);
  const maxContribucion = Math.max(...factores.map((f) => f.contribution), 0.0001);

  return (
    <div className="desglose">
      <div className="desglose-encabezado">
        <div>
          <span className="desglose-etiqueta">Proveedor óptimo</span>
          <h3>
            {elegido.name} — {formatearEta(elegido.etaMinutes)}
          </h3>
        </div>
        <div className="desglose-puntaje">
          <span className="desglose-etiqueta">Puntaje</span>
          <strong>{totalScore.toFixed(4)}</strong>
        </div>
      </div>

      <dl className="desglose-meta">
        <div>
          <dt>N° de caso</dt>
          <dd>{caseId}</dd>
        </div>
        <div>
          <dt>Decidido</dt>
          <dd>{new Date(decidedAt).toLocaleString('es-CO')}</dd>
        </div>
      </dl>

      <table className="tabla tabla-factores">
        <caption className="sr-only">Factores del puntaje para el proveedor elegido</caption>
        <thead>
          <tr>
            <th scope="col">Factor</th>
            <th scope="col">Valor</th>
            <th scope="col">Peso</th>
            <th scope="col">Contribución</th>
          </tr>
        </thead>
        <tbody>
          {factores.map((factor) => {
            const unidad = UNIDAD_FACTOR[factor.factor] ?? '';
            const ancho = Math.min(100, (factor.contribution / maxContribucion) * 100);

            return (
              <tr key={factor.factor}>
                <td data-etiqueta="Factor">{ETIQUETA_FACTOR[factor.factor] ?? factor.factor}</td>
                <td data-etiqueta="Valor">
                  {factor.raw.toFixed(2)}
                  {unidad && ` ${unidad}`}
                </td>
                <td data-etiqueta="Peso">{Math.round(factor.weight * 100)}%</td>
                <td data-etiqueta="Contribución">
                  {/* Barra proporcional: el peso de cada factor se ve sin leer los números. */}
                  <span className="barra-contenedor">
                    <span className="barra" style={{ width: `${ancho}%` }} />
                    <span className="barra-valor">{factor.contribution.toFixed(4)}</span>
                  </span>
                </td>
              </tr>
            );
          })}
          <tr className="fila fila-total">
            <td data-etiqueta="Factor">
              <strong>Total</strong>
            </td>
            <td data-etiqueta="Valor" />
            <td data-etiqueta="Peso" />
            <td data-etiqueta="Contribución">
              <strong>{totalScore.toFixed(4)}</strong>
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  );
}
