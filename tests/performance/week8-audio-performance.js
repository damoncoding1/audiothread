import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';

const concurrentUsers = Number.parseInt(__ENV.VUS || '4', 10);
const audioFilePath = __ENV.AUDIO_FILE;
const baseUrl = (__ENV.BASE_URL || 'http://localhost:5270').replace(/\/+$/, '');
const pollIntervalSeconds = Number.parseFloat(__ENV.POLL_INTERVAL_SECONDS || '0.5');
const jobTimeoutMilliseconds = Number.parseInt(__ENV.JOB_TIMEOUT_MS || '900000', 10);

if (!Number.isInteger(concurrentUsers) || concurrentUsers <= 0) {
    throw new Error('VUS must be a positive integer.');
}

if (!audioFilePath) {
    throw new Error('AUDIO_FILE must point to a local MP3 or WAV file.');
}

const audioFileName = __ENV.AUDIO_FILE_NAME || getFileName(audioFilePath);
const audioContentType = getAudioContentType(audioFileName);
const audioFileBytes = open(audioFilePath, 'b');
const completedJobs = new Rate('audio_jobs_completed');
const jobCompletionDuration = new Trend('audio_job_completion_duration', true);
const serverJobDuration = new Trend('audio_job_server_duration', true);
const compressionDuration = new Trend('audio_compression_duration', true);
const transcriptionDuration = new Trend('audio_transcription_duration', true);
const summarizationDuration = new Trend('audio_summarization_duration', true);

export const options = {
    scenarios: {
        concurrentAudioJobs: {
            executor: 'per-vu-iterations',
            vus: concurrentUsers,
            iterations: 1,
            maxDuration: __ENV.MAX_DURATION || '20m',
        },
    },
    thresholds: {
        audio_jobs_completed: ['rate==1'],
        checks: ['rate==1'],
    },
    summaryTrendStats: ['avg', 'min', 'med', 'max', 'p(90)', 'p(95)'],
};

export default function () {
    const startedAt = Date.now();
    const createResponse = http.post(
        `${baseUrl}/api/audio-files/jobs`,
        {
            file: http.file(audioFileBytes, audioFileName, audioContentType),
        },
        {
            timeout: '2m',
            tags: { operation: 'create-audio-job' },
        },
    );

    const accepted = check(createResponse, {
        'job creation returns 202': (response) => response.status === 202,
    });

    if (!accepted) {
        completedJobs.add(false);
        return;
    }

    const createdJob = createResponse.json();
    const jobId = createdJob?.id;

    if (typeof jobId !== 'string' || jobId.length === 0) {
        check(null, { 'job response includes an id': () => false });
        completedJobs.add(false);
        return;
    }

    while (Date.now() - startedAt < jobTimeoutMilliseconds) {
        const jobResponse = http.get(`${baseUrl}/api/audio-files/jobs/${jobId}`, {
            timeout: '30s',
            tags: { operation: 'get-audio-job' },
        });

        if (jobResponse.status === 200) {
            const job = jobResponse.json();

            if (job.status === 'Completed') {
                const succeeded = check(job, {
                    'job completes successfully': (snapshot) => snapshot.result !== null,
                    'processed content is AAC': (snapshot) => snapshot.result?.contentType === 'audio/aac',
                    'job includes a summary': (snapshot) =>
                        typeof snapshot.result?.summary === 'string' && snapshot.result.summary.length > 0,
                });

                completedJobs.add(succeeded);
                jobCompletionDuration.add(Date.now() - startedAt);
                addServerMetrics(job);
                return;
            }

            if (job.status === 'Failed') {
                check(job, { 'job does not fail': () => false });
                completedJobs.add(false);
                return;
            }
        }

        sleep(pollIntervalSeconds);
    }

    check(null, { 'job completes before the timeout': () => false });
    completedJobs.add(false);
}

function addServerMetrics(job) {
    const createdAt = Date.parse(job.createdAtUtc);
    const completedAt = Date.parse(job.completedAtUtc);

    if (Number.isFinite(createdAt) && Number.isFinite(completedAt)) {
        serverJobDuration.add(completedAt - createdAt);
    }

    const stepMetrics = {
        compression: compressionDuration,
        transcription: transcriptionDuration,
        summarization: summarizationDuration,
    };

    for (const step of job.steps || []) {
        const metric = stepMetrics[step.step];

        if (metric && Number.isFinite(step.durationMilliseconds)) {
            metric.add(step.durationMilliseconds);
        }
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

    throw new Error(`Unsupported audio extension: .${extension || ''}`);
}
