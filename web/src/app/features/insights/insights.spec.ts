import { niceMax, percent } from './insights';

describe('insights helpers', () => {
  it('rounds the chart axis up to a tidy number', () => {
    expect(niceMax(0)).toBe(4);
    expect(niceMax(3)).toBe(4);
    expect(niceMax(5)).toBe(5);
    expect(niceMax(6)).toBe(10);
    expect(niceMax(12)).toBe(20);
    expect(niceMax(21)).toBe(50);
    expect(niceMax(51)).toBe(100);
    expect(niceMax(100)).toBe(100);
  });

  it('never returns an axis lower than the data', () => {
    for (let v = 0; v <= 400; v++) expect(niceMax(v)).toBeGreaterThanOrEqual(v);
  });

  it('shows fractions as whole percentages', () => {
    expect(percent(0)).toBe('0%');
    expect(percent(17 / 57)).toBe('30%');
    expect(percent(1)).toBe('100%');
  });
});
