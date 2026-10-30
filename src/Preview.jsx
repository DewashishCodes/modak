// The dark "mini terminal" inside the agent tile: a diff, a command with its output, or the final answer.
export default function Preview({ preview, working }) {
  if (!preview) return <div className="term dim">{working ? <Cursor /> : 'Waiting for Claude…'}</div>

  if (preview.kind === 'diff') {
    return (
      <div className="term">
        <div className="term-file">{preview.file}</div>
        {preview.lines.map(([sign, text], i) => (
          <div key={i} className={`diff ${sign === '-' ? 'del' : 'add'}`}>
            <span className="sign">{sign}</span>{text || ' '}
          </div>
        ))}
      </div>
    )
  }

  if (preview.kind === 'term') {
    return (
      <div className="term">
        <div><span className="prompt">$</span> {preview.command}</div>
        {preview.output.map((l, i) => <div key={i} className="out">{l}</div>)}
        {!preview.output.length && working && <Cursor />}
      </div>
    )
  }

  return <div className="term answer">{preview.text}</div>
}

const Cursor = () => <span className="cursor" />
