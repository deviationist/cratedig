import api from './api';

export const getAll = async () => {
  return (await api.get('/searches')).data;
};

export const stop = ({ id }) => {
  return api.put(`/searches/${encodeURIComponent(id)}`);
};

export const remove = ({ id }) => {
  return api.delete(`/searches/${encodeURIComponent(id)}`);
};

export const create = ({ id, searchText }) => {
  return api.post('/searches', { id, searchText });
};

export const getStatus = async ({ id, includeResponses = false }) => {
  return (
    await api.get(
      `/searches/${encodeURIComponent(id)}?includeResponses=${includeResponses}`,
    )
  ).data;
};

export const getResponses = async ({ id }) => {
  const response = (
    await api.get(`/searches/${encodeURIComponent(id)}/responses`)
  ).data;

  if (!Array.isArray(response)) {
    console.warn('got non-array response from searches API', response);
    return undefined;
  }

  return response;
};

/*
 * The responses of a search in progress arrive in chunks, each numbered with
 * the position of its first response: a snapshot when the page subscribes
 * (from 0), then what arrived since, every half second or so. Positions never
 * change, so a chunk that overlaps what is already held contributes only its
 * tail, and one that starts beyond it -- possible when the first streamed
 * chunk overtakes the snapshot, or when a chunk was lost -- waits until the
 * gap is filled.
 *
 * The last chunk is marked complete and carries the total, sent once the
 * responses are saved: holding that many means holding them all.
 *
 * buffer: { responses, pending, total }, starting from emptyResponseBuffer
 */
export const emptyResponseBuffer = {
  pending: [],
  responses: [],
  total: undefined,
};

export const isResponseStreamComplete = (buffer) =>
  buffer.total !== undefined && buffer.responses.length >= buffer.total;

export const addResponseChunk = (buffer, chunk) => {
  let { responses } = buffer;
  const total = chunk.complete ? chunk.total : buffer.total;
  let pending = [...buffer.pending, chunk];
  let consumed = true;

  while (consumed) {
    consumed = false;
    const waiting = [];

    for (const candidate of pending) {
      if (candidate.from <= responses.length) {
        const fresh = candidate.responses.slice(
          responses.length - candidate.from,
        );

        if (fresh.length > 0) {
          responses = [...responses, ...fresh];
        }

        consumed = true;
      } else {
        waiting.push(candidate);
      }
    }

    pending = waiting;
  }

  return { pending, responses, total };
};

const getNthMatch = (string, regex, n) => {
  const match = string.match(regex);

  if (match) {
    return Number.parseInt(match[n], 10);
  }

  return undefined;
};

export const parseFiltersFromString = (string) => {
  const filters = {
    exclude: [],
    include: [],
    isCBR: false,
    isLossless: false,
    isLossy: false,
    isVBR: false,
    minBitDepth: 0,
    minBitRate: 0,
    minFilesInFolder: 0,
    minFileSize: 0,
    minLength: 0,
  };

  filters.minBitRate =
    getNthMatch(string, /(minbr|minbitrate):(\d+)/iu, 2) || filters.minBitRate;
  filters.minBitDepth =
    getNthMatch(string, /(minbd|minbitdepth):(\d+)/iu, 2) ||
    filters.minBitDepth;
  filters.minFileSize =
    getNthMatch(string, /(minfs|minfilesize):(\d+)/iu, 2) ||
    filters.minFileSize;
  filters.minLength =
    getNthMatch(string, /(minlen|minlength):(\d+)/iu, 2) || filters.minLength;
  filters.minFilesInFolder =
    getNthMatch(string, /(minfif|minfilesinfolder):(\d+)/iu, 2) ||
    filters.minFilesInFolder;

  filters.isVBR = Boolean(/isvbr/iu.test(string));
  filters.isCBR = Boolean(/iscbr/iu.test(string));
  filters.isLossless = Boolean(/islossless/iu.test(string));
  filters.isLossy = Boolean(/islossy/iu.test(string));

  const terms = string
    .toLowerCase()
    .split(' ')
    .filter(
      (term) =>
        !term.includes(':') &&
        term !== 'isvbr' &&
        term !== 'iscbr' &&
        term !== 'islossless' &&
        term !== 'islossy',
    );

  filters.include = terms.filter((term) => !term.startsWith('-'));
  filters.exclude = terms
    .filter((term) => term.startsWith('-'))
    .map((term) => term.slice(1));

  return filters;
};

export const filterResponse = ({
  filters = {
    exclude: [],
    include: [],
    isCBR: false,
    isLossless: false,
    isLossy: false,
    isVBR: false,
    minBitDepth: 0,
    minBitRate: 0,
    minFileSize: 0,
    minLength: 0,
  },
  response = {
    files: [],
    lockedFiles: [],
  },
}) => {
  const { files = [], lockedFiles = [] } = response;

  if (
    response.fileCount + response.lockedFileCount <
    filters.minFilesInFolder
  ) {
    return { ...response, files: [] };
  }

  const filterFiles = (filesToFilter) =>
    filesToFilter.filter((file) => {
      const {
        bitRate,
        size,
        length,
        filename,
        sampleRate,
        bitDepth,
        isVariableBitRate,
      } = file;
      const {
        isCBR,
        isVBR,
        isLossless,
        isLossy,
        minBitRate,
        minBitDepth,
        minFileSize,
        minLength,
        include = [],
        exclude = [],
      } = filters;

      if (isCBR && (isVariableBitRate === undefined || isVariableBitRate))
        return false;
      if (isVBR && (isVariableBitRate === undefined || !isVariableBitRate))
        return false;
      if (isLossless && (!sampleRate || !bitDepth)) return false;
      if (isLossy && (sampleRate || bitDepth)) return false;
      if (bitRate < minBitRate) return false;
      if (bitDepth < minBitDepth) return false;
      if (size < minFileSize) return false;
      if (length < minLength) return false;

      if (
        include.length > 0 &&
        include.filter((term) => filename.toLowerCase().includes(term))
          .length !== include.length
      ) {
        return false;
      }

      if (exclude.some((term) => filename.toLowerCase().includes(term)))
        return false;

      return true;
    });

  const filteredFiles = filterFiles(files);
  const filteredLockedFiles = filterFiles(lockedFiles);

  return {
    ...response,
    fileCount: filteredFiles.length,
    files: filteredFiles,
    lockedFileCount: filteredLockedFiles.length,
    lockedFiles: filteredLockedFiles,
  };
};
