// Local, in-memory Monaco edit proposal. Never writes to disk or invokes agents.
// A full snapshot guard prevents applying against a modified editor buffer.
export function prepareInlineEdit(file, content, start, end) {
  if (typeof file !== 'string' || !file || file.length > 255 ||
      typeof content !== 'string' || content.length > 1_000_000 ||
      !Number.isSafeInteger(start) || !Number.isSafeInteger(end) ||
      start < 0 || end < start || end > content.length || end - start > 32_768)
    throw new RangeError('Invalid inline edit selection');
  return Object.freeze({file, base:content, start, end, original:content.slice(start,end)});
}
export function applyInlineEdit(proposal, current, replacement) {
  if (!proposal || typeof current !== 'string' || current !== proposal.base ||
      current.slice(proposal.start,proposal.end) !== proposal.original)
    throw new Error('Stale buffer; inline edit was not applied');
  if (typeof replacement !== 'string' || replacement.length > 16_384 ||
      replacement.includes('\0'))
    throw new RangeError('Invalid inline edit replacement');
  const next=current.slice(0,proposal.start)+replacement+current.slice(proposal.end);
  if (next.length > 1_000_000) throw new RangeError('Edited buffer exceeds limit');
  return next;
}
