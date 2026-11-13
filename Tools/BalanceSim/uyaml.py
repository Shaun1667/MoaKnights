"""Unity YAML(데이터 에셋)만 읽는 작은 파서. 블록 매핑 · 블록 리스트(부모 키와 같은 들여쓰기) · 스칼라 · 따옴표 · {…} 인라인(문자열로 둠)."""
import re


def _scalar(v):
    v = v.strip()
    if v == '':
        return ''
    if v[0] in "'\"":
        q = v[0]
        body = v[1:-1]
        return body.replace("''", "'") if q == "'" else body
    if re.fullmatch(r'-?\d+', v):
        return int(v)
    if re.fullmatch(r'-?\d*\.\d+(e-?\d+)?|-?\d+e-?\d+', v):
        return float(v)
    return v


def parse(path):
    lines = []
    for raw in open(path, encoding='utf-8').read().splitlines():
        if raw.startswith('%') or raw.startswith('---') or not raw.strip():
            continue
        lines.append(raw)
    pos = [0]

    def indent(s):
        return len(s) - len(s.lstrip(' '))

    def parse_block(ind):
        # 매핑 (들여쓰기 ind) 또는 리스트 (ind에서 '- ')
        if pos[0] < len(lines) and lines[pos[0]].lstrip().startswith('- ') and indent(lines[pos[0]]) == ind:
            return parse_list(ind)
        return parse_map(ind)

    def parse_list(ind):
        out = []
        while pos[0] < len(lines):
            line = lines[pos[0]]
            if indent(line) != ind or not line.lstrip().startswith('-'):
                break
            rest = line.lstrip()[1:].lstrip()
            if ':' in rest and not rest.startswith(("'", '"', '{')):
                # 리스트 항목이 매핑: 첫 줄을 ind+2 매핑의 첫 줄처럼 다룬다
                lines[pos[0]] = ' ' * (ind + 2) + rest
                out.append(parse_map(ind + 2))
            else:
                out.append(_scalar(rest))
                pos[0] += 1
        return out

    def parse_map(ind):
        out = {}
        while pos[0] < len(lines):
            line = lines[pos[0]]
            if indent(line) != ind or line.lstrip().startswith('- '):
                break
            key, _, val = line.strip().partition(':')
            pos[0] += 1
            if val.strip() == '' and pos[0] < len(lines):
                nxt = lines[pos[0]]
                if indent(nxt) > ind:
                    out[key] = parse_block(indent(nxt))
                    continue
                if indent(nxt) == ind and nxt.lstrip().startswith('- '):
                    out[key] = parse_list(ind)
                    continue
                out[key] = ''
            else:
                out[key] = _scalar(val)
        return out

    root = parse_map(0)
    return list(root.values())[0]
