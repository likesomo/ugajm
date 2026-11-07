// Proposed stage order with the statue-count door. Used only for the predicted curve until core implements the door.
// Lead's request (7lucky 37c99f7): 10 (두 개의 문) to the end of chapter 2, statue doors in chapter 3, 11·12 before the finale.
// Recommended after comparing orders (design/levels/proposal-statue-door.md): keep 9 → 10, then the three statue doors,
// then 칠칠의 방 before 떠나온 자리. LEAD_ORDER is the literal request, kept for comparison.
import { LEVELS as V0 } from '../../src/levels.js';
import { STAGES } from './stages.mjs';

const by = (name) => V0.find((l) => l.name === name) ?? STAGES.find((l) => l.name === name);
const HEAD = ['일곱 걸음', '늦게 도착하기', '돌이 된 몸', '새 출발점', '몸으로 놓는 다리', '숫자 문', '홀수의 저주', '멈춰 세우는 석상'];
const DOORS = ['하나만 남겨라', '열쇠가 되는 몸', '남기기 전에 지나가라'];
export const LEAD_ORDER = [...HEAD, '두 개의 문', '한 번만 버티는 바닥', ...DOORS, '떠나온 자리', '칠칠의 방'].map(by);
export const LEVELS = [...HEAD, '한 번만 버티는 바닥', '두 개의 문', ...DOORS, '칠칠의 방', '떠나온 자리'].map(by);
