import { createEnterToSearch, DOUBLE_ENTER_MS } from './searchEnter';

const setup = ({ fail = false } = {}) => {
  let clock = 0;
  let resolveCreate;
  const created = [];
  const opened = [];

  const create = jest.fn(
    (text) =>
      new Promise((resolve) => {
        created.push(text);
        resolveCreate = () =>
          resolve(fail ? undefined : `id-${created.length}`);
      }),
  );

  const enter = createEnterToSearch({
    create,
    now: () => clock,
    open: (id) => opened.push(id),
  });

  return {
    advance: (ms) => {
      clock += ms;
    },
    created,
    enter,
    finish: async () => {
      resolveCreate();
      await Promise.resolve();
      await Promise.resolve();
    },
    opened,
  };
};

describe('createEnterToSearch', () => {
  it('starts a search on one Enter and stays put', async () => {
    expect.hasAssertions();
    const s = setup();
    const done = s.enter('artist');
    await s.finish();
    await done;

    expect(s.created).toEqual(['artist']);
    expect(s.opened).toEqual([]);
  });

  it('opens the search on a second Enter on the cleared field', async () => {
    expect.hasAssertions();
    const s = setup();
    const done = s.enter('artist');
    await s.finish();
    await done;

    s.advance(300);
    await s.enter('');

    expect(s.opened).toEqual(['id-1']);
  });

  it('opens it, rather than searching again, when the second Enter beats the create', async () => {
    expect.hasAssertions();
    const s = setup();
    const done = s.enter('artist');
    // field not cleared yet: the text is still there
    s.enter('artist');
    await s.finish();
    await done;

    expect(s.created).toEqual(['artist']);
    expect(s.opened).toEqual(['id-1']);
  });

  it('counts the window from the first Enter, not from when the search exists', async () => {
    expect.hasAssertions();
    const s = setup();
    const done = s.enter('artist');
    s.advance(DOUBLE_ENTER_MS - 100);
    await s.finish();
    await done;

    s.advance(200);
    await s.enter('');

    expect(s.opened).toEqual([]);
  });

  it('does nothing on a late second Enter on an empty field', async () => {
    expect.hasAssertions();
    const s = setup();
    const done = s.enter('artist');
    await s.finish();
    await done;

    s.advance(DOUBLE_ENTER_MS + 1);
    await s.enter('');

    expect(s.created).toEqual(['artist']);
    expect(s.opened).toEqual([]);
  });

  it('starts a new search on a late Enter with new text', async () => {
    expect.hasAssertions();
    const s = setup();
    let done = s.enter('one');
    await s.finish();
    await done;

    s.advance(DOUBLE_ENTER_MS + 1);
    done = s.enter('two');
    await s.finish();
    await done;

    expect(s.created).toEqual(['one', 'two']);
    expect(s.opened).toEqual([]);
  });

  it('opens nothing when the create failed', async () => {
    expect.hasAssertions();
    const s = setup({ fail: true });
    const done = s.enter('artist');
    s.enter('artist');
    await s.finish();
    await done;

    await s.enter('');

    expect(s.opened).toEqual([]);
  });
});
