import http from 'k6/http';
import { check } from 'k6';
import { Rate } from 'k6/metrics';

const concurrentRequests = Number.parseInt(__ENV.VUS || '5', 10);
const audioFilePath = __ENV.AUDIO_FILE;
const baseUrl = (__ENV.BASE_URL || 'http://localhost:5270').replace(/\/+$/, '');
const requestTimeout = __ENV.REQUEST_TIMEOUT || '3m';

if (!Number.isInteger(concurrentRequests) || concurrentRequests <= 0) {
    throw new Error('VUS must be a positive integer.');
}

if (!audioFilePath) {
    throw new Error('AUDIO_FILE is required and must point to a local MP3 or WAV file.');
}

const audioFileName = __ENV.AUDIO_FILE_NAME || getFileName(audioFilePath);
const audioContentType = getAudioContentType(audioFileName);
const audioFileBytes = open(audioFilePath, 'b');
const uploadSuccessRate = new Rate('audio_upload_success');

export const options = {
    scenarios: {
        concurrentAudioUploads: {
            executor: 'per-vu-iterations',
            vus: concurrentRequests,
            iterations: 1,
            maxDuration: __ENV.MAX_DURATION || '4m',
        },
    },
    thresholds: {
        checks: ['rate==1'],
        http_req_failed: ['rate==0'],
        audio_upload_success: ['rate==1'],
    },
    summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)'],
};

export default function () {
    const response = http.post(
        `${baseUrl}/api/audio-files`,
        {
            file: http.file(audioFileBytes, audioFileName, audioContentType),
        },
        {
            timeout: requestTimeout,
            tags: {
                endpoint: 'audio-upload',
                concurrent_requests: String(concurrentRequests),
            },
        },
    );

    let responseBody = null;
    try {
        responseBody = response.json();
    } catch {
        // The checks below report an invalid or empty API response.
    }

    const uploadSucceeded = check(response, {
        'upload returns 201': (result) => result.status === 201,
        'response content type is AAC': () => responseBody?.contentType === 'audio/aac',
        'stored file path ends in .aac': () => responseBody?.storagePath?.endsWith('.aac') === true,
        'response includes an audio UUID': () => typeof responseBody?.id === 'string' && responseBody.id.length > 0,
    });

    uploadSuccessRate.add(uploadSucceeded);

    if (!uploadSucceeded) {
        console.error(`Upload failed with status ${response.status}: ${response.body}`);
    }
}

function getFileName(path) {
    const pathSegments = path.split(/[\\/]/);
    return pathSegments[pathSegments.length - 1];
}

function getAudioContentType(fileName) {
    const extension = fileName.split('.').pop()?.toLowerCase();

    if (extension === 'mp3') {
        return 'audio/mpeg';
    }

    if (extension === 'wav') {
        return 'audio/wav';
    }

    throw new Error(`Unsupported load-test audio extension: .${extension || ''}`);
}
