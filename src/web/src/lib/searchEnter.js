/*
 * Enter in a search field: once starts a search and stays put, clearing the
 * field for the next; twice in quick succession opens the search the first
 * one started. The search icon does both in one go, but on a phone the
 * keyboard's own key is where the thumb is.
 *
 * The second Enter often lands while the first is still being created, with
 * the text still in the field -- so while a search is in flight, any Enter is
 * a request to open it once it exists, never a second search.
 */

// how soon after the first Enter a second one counts as asking to open the
// search it started (one still being created always counts)
export const DOUBLE_ENTER_MS = 1_000;

/*
 * create: (searchText) => Promise<id | undefined>, undefined when it failed
 * open:   (id) => void
 */
export const createEnterToSearch = ({ create, open, now = Date.now }) => {
  // { id, at } once created; { pending: true, at, openWhenCreated } while
  // creating -- at is when the Enter that started it was pressed
  let last = null;

  return async (value) => {
    if (last?.pending) {
      last.openWhenCreated = true;
      return;
    }

    if (last && now() - last.at < DOUBLE_ENTER_MS) {
      const opening = last.id;
      last = null;
      open(opening);
      return;
    }

    last = null;

    // a second Enter that came too late is not a request to search for nothing
    if (!value) return;

    const entry = { at: now(), openWhenCreated: false, pending: true };
    last = entry;

    const id = await create(value);

    if (last !== entry) return;

    if (!id) {
      last = null;
      return;
    }

    if (entry.openWhenCreated) {
      last = null;
      open(id);
      return;
    }

    last = { at: entry.at, id };
  };
};
