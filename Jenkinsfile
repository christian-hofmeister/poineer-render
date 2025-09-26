// Jenkins Declarative Pipeline for POIneer.Render
// - Builds & tests on 'develop'
// - Placeholder "deploy" on 'release/*'
// - Avoids heavy rendering on Jenkins
// - Uses repository script to ensure dotnet SDK if missing

pipeline {
  agent any

  options {
    ansiColor('xterm')
    timestamps()
    disableConcurrentBuilds()
    buildDiscarder(logRotator(numToKeepStr: '20', artifactNumToKeepStr: '10'))
    timeout(time: 30, unit: 'MINUTES')
  }

  environment {
    // Speed up dotnet CLI
    DOTNET_CLI_TELEMETRY_OPTOUT = '1'
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
    NUGET_PACKAGES = "${WORKSPACE}/.nuget/packages"
    // Desired SDK version for the project; adjust if you bump SDK
    DOTNET_SDK_VERSION = '9.0.305'
    // Project paths
    RENDER_CSProj = 'src/POIneer.Render/POIneer.Render.csproj'
    PUBLISH_DIR = 'out/POIneer.Render'
  }

  stages {

    stage('Checkout') {
      steps {
        checkout scm
        sh 'git --no-pager log -1 --pretty=fuller'
      }
    }



    stage('Ensure dotnet SDK') {
      steps {
        sh '''
          set -eux
          SDK="${DOTNET_SDK_VERSION}"
          INSTALL_DIR="${WORKSPACE}/.dotnet"

          mkdir -p "${INSTALL_DIR}"
          curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh

          # nur installieren, wenn die gewünschte SDK-Version fehlt
          if ! [ -x "${INSTALL_DIR}/dotnet" ] || ! "${INSTALL_DIR}/dotnet" --list-sdks | grep -q "^${SDK} "; then
            bash /tmp/dotnet-install.sh --version "${SDK}" --install-dir "${INSTALL_DIR}" --skip-non-versioned-files
          fi

          "${INSTALL_DIR}/dotnet" --info
        '''
        script {
          env.DOTNET = "${WORKSPACE}/.dotnet/dotnet"
          env.DOTNET_ROOT = "${WORKSPACE}/.dotnet"
          env.PATH = "${WORKSPACE}/.dotnet:${env.PATH}"
        }
      }
    }



    stage('Debug dotnet') {
      steps {
        sh '''
          set -eux
          which -a dotnet || true
          echo "PATH=$PATH"
          echo "DOTNET_ROOT=${DOTNET_ROOT-}"
          dotnet --version || true
          "$DOTNET" --version
          "$DOTNET" --list-sdks
        '''
      }
    }

    stage('Restore') {
      steps {
        sh '''
          set -eux
          mkdir -p "${NUGET_PACKAGES}"
          "$DOTNET" restore "${RENDER_CSProj}" --nologo
        '''
      }
    }

    stage('Build') {
      steps {
        sh '''
          set -eux
          "$DOTNET" build "${RENDER_CSProj}" -c Release --no-restore --nologo
        '''
      }
    }

    stage('Test') {
      when {
        branch 'develop'
      }
      steps {
        sh '''
          set -eu
          [ -f "${WORKSPACE}/.env-dotnet" ] && set -a && . "${WORKSPACE}/.env-dotnet" && set +a
          RS_ARG=""
          [ -f coverlet.runsettings ] && RS_ARG="--settings coverlet.runsettings --collect:\"XPlat Code Coverage\""
          "$DOTNET" test POIneerRender.sln -c Release --no-build --nologo --logger "trx;LogFileName=test-results.trx" ${RS_ARG}
        '''
      }
      post {
        always {
          // Archive any TRX files so you can review them
          archiveArtifacts artifacts: '**/TestResults/**/*.trx', fingerprint: true, onlyIfSuccessful: false
        }
      }
    }

    stage('Publish (App)') {
      steps {
        sh '''
          set -eux
          rm -rf "${PUBLISH_DIR}"
          "$DOTNET" publish "${RENDER_CSProj}" -c Release -o "${PUBLISH_DIR}" --no-build --nologo
          tar -C "${PUBLISH_DIR}" -czf "poineer-render_${BRANCH_NAME}.tar.gz" .
        '''
      }
      post {
        success {
          archiveArtifacts artifacts: 'poineer-render_*.tar.gz', fingerprint: true
        }
      }
    }

    stage('Deploy (placeholder)') {
      when {
        expression {
          // Run on branches like release/1.0.0, release/v0.3, etc.
          return env.BRANCH_NAME ==~ /release\/.+/
        }
      }
      steps {
        sh '''
          set -eux
          echo "[deploy] Release branch detected: ${BRANCH_NAME}"
          echo "[deploy] This is a placeholder. No heavy rendering and no live deploy performed on Jenkins."
          echo "[deploy] Here you could: rsync the published app to your render server, trigger a job, etc."
          # Example (commented):
          # rsync -avz --delete "${PUBLISH_DIR}/" user@render-host:/opt/poineer/render/
          # ssh user@render-host 'sudo systemctl restart poineer-render.service'
        '''
      }
    }

    stage('(Optional) Dry-Run Render Check') {
      when {
        allOf {
          branch 'develop'
          expression { return params?.RUN_RENDER_CHECK == true }
        }
      }
      steps {
        sh '''
          set -eux
          echo "[check] Running a short sanity-check invocation (no heavy work expected)."
          # Example of a quick, no-op style run if your app supports it:
          # "$DOTNET" "${PUBLISH_DIR}/POIneer.Render.dll" --help
        '''
      }
    }
  }

  parameters {
    // If you ever want to trigger a lightweight health check manually on develop
    booleanParam(name: 'RUN_RENDER_CHECK', defaultValue: false, description: 'Run a quick, no-op sanity check on develop (no heavy rendering).')
  }

  post {
    always {
      script {
        def emoji = currentBuild.currentResult == 'SUCCESS' ? '✅' : (currentBuild.currentResult == 'UNSTABLE' ? '⚠️' : '❌')
        echo "${emoji} Build result: ${currentBuild.currentResult}"
      }
    }
    failure {
      echo "Build failed. See logs above."
    }
  }
}
